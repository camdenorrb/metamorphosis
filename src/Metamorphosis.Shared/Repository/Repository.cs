using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Metamorphosis.Objects;
using Metamorphosis.Utilities;

namespace Metamorphosis.Repository
{
    public sealed class Repository : IDisposable
    {
        private readonly string _commitsDir;
        private readonly SQLiteConnection _db;
        private string _currentBranch;

        public string RepoDirectory { get; }
        public string CurrentBranch => _currentBranch;

        private Repository(string repoDir)
        {
            RepoDirectory = repoDir;
            _commitsDir = Path.Combine(repoDir, "commits");
            Directory.CreateDirectory(_commitsDir);

            string dbPath = Path.Combine(repoDir, "repository.meta.db");
            bool isNew = !File.Exists(dbPath);
            _db = new SQLiteConnection($"Data Source={dbPath};Version=3;");
            _db.Open();

            if (isNew) InitializeSchema();

            _currentBranch = GetConfig("current_branch") ?? "main";
        }

        public static Repository OpenOrCreate(string modelPath)
            => new Repository(ResolveRepoDirectory(modelPath));

        public CommitInfo Commit(string snapshotPath, string message, string author)
        {
            string hash = DataUtility.ComputeFileHash(snapshotPath);
            CommitInfo? parent = GetHead();
            var now = DateTime.UtcNow;

            using var tx = _db.BeginTransaction();

            var ins = _db.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText =
                "INSERT INTO commits (parent_id, branch, hash, message, author, machine, timestamp, timestamp_ticks, snapshot_file) " +
                "VALUES (@pid, @branch, @hash, @message, @author, @machine, @ts, @ticks, '')";
            ins.Parameters.AddWithValue("@pid", (object?)parent?.Id ?? DBNull.Value);
            ins.Parameters.AddWithValue("@branch", _currentBranch);
            ins.Parameters.AddWithValue("@hash", hash);
            ins.Parameters.AddWithValue("@message", message);
            ins.Parameters.AddWithValue("@author", author);
            ins.Parameters.AddWithValue("@machine", Environment.MachineName);
            ins.Parameters.AddWithValue("@ts", now.ToString("o"));
            ins.Parameters.AddWithValue("@ticks", now.Ticks);
            ins.ExecuteNonQuery();

            long id = _db.LastInsertRowId;
            string snapshotFile = $"{id}_{hash.Substring(0, 8)}.sdb";
            File.Copy(snapshotPath, Path.Combine(_commitsDir, snapshotFile), overwrite: true);

            var upd = _db.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = "UPDATE commits SET snapshot_file = @sf WHERE id = @id";
            upd.Parameters.AddWithValue("@sf", snapshotFile);
            upd.Parameters.AddWithValue("@id", id);
            upd.ExecuteNonQuery();

            var bUpd = _db.CreateCommand();
            bUpd.Transaction = tx;
            bUpd.CommandText = "UPDATE branches SET head_id = @head WHERE name = @name";
            bUpd.Parameters.AddWithValue("@head", id);
            bUpd.Parameters.AddWithValue("@name", _currentBranch);
            bUpd.ExecuteNonQuery();

            tx.Commit();

            return GetById(id)!;
        }

        public CommitInfo? GetHead()
        {
            var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT head_id FROM branches WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", _currentBranch);
            var raw = cmd.ExecuteScalar();
            if (raw == null || raw == DBNull.Value) return null;
            return GetById(Convert.ToInt64(raw));
        }

        public CommitInfo? GetById(long id)
        {
            var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT * FROM commits WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadCommit(reader) : null;
        }

        public IList<CommitInfo> GetLog(string? branch = null)
        {
            var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT * FROM commits WHERE branch = @branch ORDER BY id DESC";
            cmd.Parameters.AddWithValue("@branch", branch ?? _currentBranch);
            using var reader = cmd.ExecuteReader();
            var list = new List<CommitInfo>();
            while (reader.Read()) list.Add(ReadCommit(reader));
            return list;
        }

        public void CreateBranch(string name, long? fromCommitId = null)
        {
            long? headId = fromCommitId ?? GetHead()?.Id;
            var cmd = _db.CreateCommand();
            cmd.CommandText = "INSERT INTO branches (name, head_id, created_at) VALUES (@name, @head, @created)";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@head", (object?)headId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public void CheckoutBranch(string name)
        {
            var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT name FROM branches WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            if (cmd.ExecuteScalar() == null)
                throw new InvalidOperationException($"Branch '{name}' does not exist.");
            _currentBranch = name;
            SetConfig("current_branch", name);
        }

        public IList<BranchInfo> GetBranches()
        {
            var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT name, head_id, created_at FROM branches ORDER BY created_at";
            using var reader = cmd.ExecuteReader();
            var list = new List<BranchInfo>();
            while (reader.Read())
                list.Add(new BranchInfo
                {
                    Name = reader.GetString(0),
                    HeadId = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1),
                    CreatedAt = reader.GetString(2)
                });
            return list;
        }

        public string? GetSnapshotPath(long commitId)
        {
            var commit = GetById(commitId);
            if (commit == null) return null;
            string path = Path.Combine(_commitsDir, commit.SnapshotFile);
            return File.Exists(path) ? path : null;
        }

        public bool GetAutoCommitEnabled()
            => string.Equals(GetConfig("auto_commit_enabled"), "true", StringComparison.OrdinalIgnoreCase);

        public void SetAutoCommitEnabled(bool enabled)
            => SetConfig("auto_commit_enabled", enabled ? "true" : "false");

        public string GetAutoCommitTemplate()
            => GetConfig("auto_commit_template") ?? "Auto-commit: {DateTime} by {User}";

        public void SetAutoCommitTemplate(string template)
            => SetConfig("auto_commit_template", template ?? "");

        private void SetConfig(string key, string value)
        {
            var cmd = _db.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO repo_config (key, value) VALUES (@key, @value)";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.ExecuteNonQuery();
        }

        private string? GetConfig(string key)
        {
            var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT CAST(value AS TEXT) FROM repo_config WHERE key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            return cmd.ExecuteScalar()?.ToString();
        }

        private void InitializeSchema()
        {
            string[]? ddl = DataUtility.ReadSQLScript("MetamorphosisCore.DBScript.RepositoryV1.sql");
            if (ddl == null) throw new InvalidOperationException("Missing embedded resource: RepositoryV1.sql");
            foreach (string sql in ddl)
                new SQLiteCommand(sql, _db).ExecuteNonQuery();
        }

        private static CommitInfo ReadCommit(SQLiteDataReader r)
        {
            int pidOrd = r.GetOrdinal("parent_id");
            int ecOrd = r.GetOrdinal("element_count");
            int mpOrd = r.GetOrdinal("model_path");
            int rvOrd = r.GetOrdinal("revit_version");
            int dgOrd = r.GetOrdinal("document_guid");
            return new CommitInfo
            {
                Id            = r.GetInt64(r.GetOrdinal("id")),
                ParentId      = r.IsDBNull(pidOrd) ? (long?)null : r.GetInt64(pidOrd),
                Branch        = r.GetString(r.GetOrdinal("branch")),
                Hash          = r.GetString(r.GetOrdinal("hash")),
                Message       = r.GetString(r.GetOrdinal("message")),
                Author        = r.GetString(r.GetOrdinal("author")),
                Machine       = r.GetString(r.GetOrdinal("machine")),
                Timestamp     = r.GetString(r.GetOrdinal("timestamp")),
                TimestampTicks= r.GetInt64(r.GetOrdinal("timestamp_ticks")),
                SnapshotFile  = r.GetString(r.GetOrdinal("snapshot_file")),
                ElementCount  = r.IsDBNull(ecOrd) ? (int?)null : r.GetInt32(ecOrd),
                ModelPath     = r.IsDBNull(mpOrd) ? null : r.GetString(mpOrd),
                RevitVersion  = r.IsDBNull(rvOrd) ? null : r.GetString(rvOrd),
                DocumentGuid  = r.IsDBNull(dgOrd) ? null : r.GetString(dgOrd),
            };
        }

        private static string ResolveRepoDirectory(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath) ||
                modelPath.StartsWith("BIM360:", StringComparison.OrdinalIgnoreCase) ||
                modelPath.StartsWith("AUTODESK DOC:", StringComparison.OrdinalIgnoreCase))
            {
                using var sha = SHA256.Create();
                string h = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(modelPath ?? "")))
                                       .Replace("-", "").ToLowerInvariant();
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Metamorphosis", h, ".metamorphosis");
            }
            string? dir = Path.GetDirectoryName(modelPath);
            if (string.IsNullOrEmpty(dir)) dir = Directory.GetCurrentDirectory();
            return Path.Combine(dir, ".metamorphosis");
        }

        public void Dispose() => _db?.Dispose();
    }
}
