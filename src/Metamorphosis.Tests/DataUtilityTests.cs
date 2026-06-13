using Metamorphosis.Utilities;
using System.Data.SQLite;
using System.IO;
using Xunit;

namespace Metamorphosis.Tests;

public class DataUtilityTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string CreateTempDb()
    {
        string path = Path.GetTempFileName();
        File.Delete(path);
        SQLiteConnection.CreateFile(path);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string f in _tempFiles)
            try { File.Delete(f); } catch { }
    }

    // ── CurrentVersion ───────────────────────────────────────────────────────

    [Fact]
    public void CurrentVersion_Is_1_1()
    {
        Assert.Equal(new Version(1, 1), DataUtility.CurrentVersion);
    }

    // ── ReadSQLScript ────────────────────────────────────────────────────────

    [Fact]
    public void ReadSQLScript_ValidResource_ReturnsNonEmptyArray()
    {
        string[] lines = DataUtility.ReadSQLScript("MetamorphosisCore.databaseFormat.txt");
        Assert.NotNull(lines);
        Assert.NotEmpty(lines);
    }

    [Fact]
    public void ReadSQLScript_ValidResource_ContainsCreateTableStatements()
    {
        string[] lines = DataUtility.ReadSQLScript("MetamorphosisCore.databaseFormat.txt");
        Assert.Contains(lines, l => l.TrimStart().StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReadSQLScript_InvalidResource_ReturnsNull()
    {
        string[]? lines = DataUtility.ReadSQLScript("MetamorphosisCore.doesNotExist.txt");
        Assert.Null(lines);
    }

    // ── UpgradeFrom ──────────────────────────────────────────────────────────

    [Fact]
    public void UpgradeFrom_AtCurrentVersion_IsNoOp()
    {
        string path = CreateTempDb();
        using var conn = new SQLiteConnection($"Data Source={path};Version=3;");
        conn.Open();

        // Create the full schema so that upgrade scripts have a valid DB to operate on.
        string[] schema = DataUtility.ReadSQLScript("MetamorphosisCore.databaseFormat.txt")!;
        foreach (string sql in schema)
            new SQLiteCommand(sql, conn).ExecuteNonQuery();

        // Seed with the current schema version.
        new SQLiteCommand(
            $"INSERT INTO _objects_header (keyword, value) VALUES ('SchemaVersion', '{DataUtility.CurrentVersion}')",
            conn).ExecuteNonQuery();

        // Calling UpgradeFrom at the current version must not modify anything.
        DataUtility.UpgradeFrom(conn, DataUtility.CurrentVersion, null);

        string? ver = new SQLiteCommand(
            "SELECT value FROM _objects_header WHERE keyword='SchemaVersion'", conn)
            .ExecuteScalar()?.ToString();

        Assert.Equal(DataUtility.CurrentVersion.ToString(), ver);
    }

    [Fact]
    public void UpgradeFrom_OldSchema_AddsHeaderTableAndVersionguidColumn()
    {
        // Build a pre-1.0 database: the tables from the original schema format
        // but without _objects_header and without the versionguid column.
        string path = CreateTempDb();
        using var conn = new SQLiteConnection($"Data Source={path};Version=3;");
        conn.Open();

        const string oldSchema = """
            CREATE TABLE "_objects_attr"("id" INTEGER,"name" TEXT,"category" TEXT,"data_type" INTEGER,"data_type_context" TEXT,"description" TEXT,"display_name" TEXT,"flags" INTEGER);
            CREATE TABLE "_objects_eav"("id" INTEGER PRIMARY KEY,"entity_id" INTEGER,"attribute_id" INTEGER,"value_id" INTEGER);
            CREATE TABLE "_objects_ercv"("id" INTEGER PRIMARY KEY,"entity_id" INTEGER,"row_id" INTEGER,"column_id" INTEGER,"value_id" INTEGER);
            CREATE TABLE "_objects_id"("id" INTEGER,"external_id" BLOB,"viewable_id" BLOB,"category" TEXT,"isType" INTEGER);
            CREATE TABLE "_objects_val"("id" INTEGER,"value" BLOB UNIQUE);
            CREATE TABLE "_objects_geom"("id" INTEGER,"BoundingBoxMin" BLOB,"BoundingBoxMax" BLOB,"Location" BLOB,"Location2" BLOB,"Level" BLOB,"Rotation" FLOAT);
            """;

        foreach (string sql in oldSchema.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            new SQLiteCommand(sql, conn).ExecuteNonQuery();

        List<string> log = new();
        DataUtility.UpgradeFrom(conn, new Version(0, 0), msg => log.Add(msg));

        // After upgrade, _objects_header must exist with SchemaVersion = 1.1
        object? tableExists = new SQLiteCommand(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='_objects_header'", conn)
            .ExecuteScalar();
        Assert.Equal("_objects_header", tableExists?.ToString());

        string? version = new SQLiteCommand(
            "SELECT value FROM _objects_header WHERE keyword='SchemaVersion'", conn)
            .ExecuteScalar()?.ToString();
        Assert.Equal("1.1", version);

        // versionguid column must have been added to _objects_id
        var colCmd = conn.CreateCommand();
        colCmd.CommandText = "PRAGMA table_info(_objects_id)";
        using var reader = colCmd.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read()) columns.Add(reader.GetString(1));
        Assert.Contains("versionguid", columns);
    }
}
