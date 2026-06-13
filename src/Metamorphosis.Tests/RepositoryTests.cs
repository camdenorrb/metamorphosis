using Metamorphosis.Objects;
using Metamorphosis.Repository;
using Metamorphosis.Utilities;
using System.IO;
using Xunit;

namespace Metamorphosis.Tests;

public class RepositoryTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private string CreateTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private string CreateFakeSnapshot(string dir)
    {
        string path = Path.Combine(dir, "snapshot.sdb");
        // Minimal valid content — just needs to be a non-empty file for hashing.
        File.WriteAllBytes(path, new byte[] { 0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66, 0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33 });
        return path;
    }

    public void Dispose()
    {
        foreach (string d in _tempDirs)
            try { Directory.Delete(d, recursive: true); } catch { }
    }

    // ── directory structure ──────────────────────────────────────────────────

    [Fact]
    public void OpenOrCreate_CreatesExpectedDirectories()
    {
        string modelDir = CreateTempDir();
        string modelPath = Path.Combine(modelDir, "Test.rvt");

        using var repo = Repository.OpenOrCreate(modelPath);

        Assert.True(Directory.Exists(Path.Combine(modelDir, ".metamorphosis")));
        Assert.True(Directory.Exists(Path.Combine(modelDir, ".metamorphosis", "commits")));
        Assert.True(File.Exists(Path.Combine(modelDir, ".metamorphosis", "repository.meta.db")));
    }

    [Fact]
    public void OpenOrCreate_IsIdempotent()
    {
        string modelDir = CreateTempDir();
        string modelPath = Path.Combine(modelDir, "Test.rvt");

        using (Repository.OpenOrCreate(modelPath)) { }
        // Second open must not throw or corrupt state.
        using var repo = Repository.OpenOrCreate(modelPath);
        Assert.NotNull(repo);
    }

    // ── commit ───────────────────────────────────────────────────────────────

    [Fact]
    public void Commit_InsertsRowAndCopiesSnapshotFile()
    {
        string modelDir = CreateTempDir();
        string snapshot = CreateFakeSnapshot(modelDir);

        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        CommitInfo c = repo.Commit(snapshot, "Initial commit", "Tester");

        Assert.Equal(1L, c.Id);
        Assert.Equal("Initial commit", c.Message);
        Assert.Equal("Tester", c.Author);
        Assert.Equal("main", c.Branch);
        Assert.Equal(64, c.Hash.Length); // SHA-256 hex

        string storedPath = Path.Combine(modelDir, ".metamorphosis", "commits", c.SnapshotFile);
        Assert.True(File.Exists(storedPath));
        Assert.Equal($"1_{c.Hash.Substring(0, 8)}.sdb", c.SnapshotFile);
    }

    [Fact]
    public void Commit_ParentIdIsSetForSecondCommit()
    {
        string modelDir = CreateTempDir();
        string snapshot = CreateFakeSnapshot(modelDir);

        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        CommitInfo c1 = repo.Commit(snapshot, "first", "A");
        CommitInfo c2 = repo.Commit(snapshot, "second", "B");

        Assert.Null(c1.ParentId);
        Assert.Equal(c1.Id, c2.ParentId);
    }

    // ── GetHead ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetHead_ReturnsNullOnEmptyRepo()
    {
        string modelDir = CreateTempDir();
        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        Assert.Null(repo.GetHead());
    }

    [Fact]
    public void GetHead_ReturnsLatestCommit()
    {
        string modelDir = CreateTempDir();
        string snapshot = CreateFakeSnapshot(modelDir);

        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        CommitInfo c1 = repo.Commit(snapshot, "first", "A");
        CommitInfo c2 = repo.Commit(snapshot, "second", "B");

        CommitInfo? head = repo.GetHead();
        Assert.NotNull(head);
        Assert.Equal(c2.Id, head!.Id);
        Assert.Equal("second", head.Message);
    }

    // ── GetLog ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetLog_ReturnsNewestFirst()
    {
        string modelDir = CreateTempDir();
        string snapshot = CreateFakeSnapshot(modelDir);

        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        repo.Commit(snapshot, "first", "A");
        repo.Commit(snapshot, "second", "B");
        repo.Commit(snapshot, "third", "C");

        IList<CommitInfo> log = repo.GetLog();
        Assert.Equal(3, log.Count);
        Assert.Equal("third", log[0].Message);
        Assert.Equal("first", log[2].Message);
    }

    [Fact]
    public void GetLog_EmptyRepoReturnsEmptyList()
    {
        string modelDir = CreateTempDir();
        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        Assert.Empty(repo.GetLog());
    }

    // ── branches ─────────────────────────────────────────────────────────────

    [Fact]
    public void NewRepo_HasMainBranchByDefault()
    {
        string modelDir = CreateTempDir();
        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        Assert.Equal("main", repo.CurrentBranch);
        Assert.Single(repo.GetBranches());
        Assert.Equal("main", repo.GetBranches()[0].Name);
    }

    [Fact]
    public void CreateBranch_And_CheckoutBranch()
    {
        string modelDir = CreateTempDir();
        string snapshot = CreateFakeSnapshot(modelDir);

        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        repo.Commit(snapshot, "base", "A");
        repo.CreateBranch("feature");
        repo.CheckoutBranch("feature");

        Assert.Equal("feature", repo.CurrentBranch);
        IList<BranchInfo> branches = repo.GetBranches();
        Assert.Equal(2, branches.Count);
        Assert.Contains(branches, b => b.Name == "main");
        Assert.Contains(branches, b => b.Name == "feature");
    }

    [Fact]
    public void CheckoutBranch_NonExistent_Throws()
    {
        string modelDir = CreateTempDir();
        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        Assert.Throws<InvalidOperationException>(() => repo.CheckoutBranch("ghost"));
    }

    [Fact]
    public void CommitOnBranch_DoesNotAffectOtherBranch()
    {
        string modelDir = CreateTempDir();
        string snapshot = CreateFakeSnapshot(modelDir);

        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        repo.Commit(snapshot, "main-commit", "A");
        repo.CreateBranch("feature");
        repo.CheckoutBranch("feature");
        repo.Commit(snapshot, "feature-commit", "B");

        IList<CommitInfo> mainLog = repo.GetLog("main");
        IList<CommitInfo> featureLog = repo.GetLog("feature");

        Assert.Single(mainLog);
        Assert.Single(featureLog);
        Assert.Equal("main-commit", mainLog[0].Message);
        Assert.Equal("feature-commit", featureLog[0].Message);
    }

    // ── GetSnapshotPath ───────────────────────────────────────────────────────

    [Fact]
    public void GetSnapshotPath_ReturnsExistingFile()
    {
        string modelDir = CreateTempDir();
        string snapshot = CreateFakeSnapshot(modelDir);

        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        CommitInfo c = repo.Commit(snapshot, "msg", "A");

        string? path = repo.GetSnapshotPath(c.Id);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void GetSnapshotPath_UnknownId_ReturnsNull()
    {
        string modelDir = CreateTempDir();
        using var repo = Repository.OpenOrCreate(Path.Combine(modelDir, "Test.rvt"));
        Assert.Null(repo.GetSnapshotPath(9999));
    }

    // ── DataUtility.ComputeFileHash ──────────────────────────────────────────

    [Fact]
    public void ComputeFileHash_IsDeterministic()
    {
        string dir = CreateTempDir();
        string path = Path.Combine(dir, "test.bin");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5 });

        string h1 = DataUtility.ComputeFileHash(path);
        string h2 = DataUtility.ComputeFileHash(path);

        Assert.Equal(h1, h2);
        Assert.Equal(64, h1.Length);
        Assert.Matches("^[0-9a-f]{64}$", h1);
    }

    [Fact]
    public void ComputeFileHash_DifferentContent_DifferentHash()
    {
        string dir = CreateTempDir();
        string p1 = Path.Combine(dir, "a.bin");
        string p2 = Path.Combine(dir, "b.bin");
        File.WriteAllBytes(p1, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(p2, new byte[] { 4, 5, 6 });

        Assert.NotEqual(DataUtility.ComputeFileHash(p1), DataUtility.ComputeFileHash(p2));
    }
}
