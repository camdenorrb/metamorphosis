using Metamorphosis.Utilities;
using System.Data.SQLite;
using Xunit;

namespace Metamorphosis.Tests;

/// <summary>
/// Verifies the SQLite schema used for snapshots: table existence, constraints,
/// parameterized insert safety, and basic EAV round-trips.
/// </summary>
public class SqliteSchemaTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private SQLiteConnection OpenSchemaDb()
    {
        string path = Path.GetTempFileName();
        File.Delete(path);
        SQLiteConnection.CreateFile(path);
        _tempFiles.Add(path);

        var conn = new SQLiteConnection($"Data Source={path};Version=3;");
        conn.Open();

        string[] schema = DataUtility.ReadSQLScript("MetamorphosisCore.databaseFormat.txt")!;
        foreach (string sql in schema)
            new SQLiteCommand(sql, conn).ExecuteNonQuery();

        return conn;
    }

    public void Dispose()
    {
        foreach (string f in _tempFiles)
            try { File.Delete(f); } catch { }
    }

    // ── table existence ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("_objects_header")]
    [InlineData("_objects_attr")]
    [InlineData("_objects_eav")]
    [InlineData("_objects_id")]
    [InlineData("_objects_val")]
    [InlineData("_objects_geom")]
    public void Schema_AllExpectedTablesExist(string tableName)
    {
        using var conn = OpenSchemaDb();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@n";
        cmd.Parameters.AddWithValue("@n", tableName);
        Assert.Equal(tableName, cmd.ExecuteScalar()?.ToString());
    }

    // ── index existence ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("_objects_eav_idx_entity_id")]
    [InlineData("_objects_ercv_idx_entity_id")]
    public void Schema_ExpectedIndexesExist(string indexName)
    {
        using var conn = OpenSchemaDb();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name=@n";
        cmd.Parameters.AddWithValue("@n", indexName);
        Assert.Equal(indexName, cmd.ExecuteScalar()?.ToString());
    }

    // ── header table ─────────────────────────────────────────────────────────

    [Fact]
    public void Header_InsertAndRead_RoundTrip()
    {
        using var conn = OpenSchemaDb();
        var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO _objects_header (keyword, value) VALUES (@k, @v)";
        ins.Parameters.AddWithValue("@k", "SchemaVersion");
        ins.Parameters.AddWithValue("@v", "1.1");
        ins.ExecuteNonQuery();

        var sel = conn.CreateCommand();
        sel.CommandText = "SELECT value FROM _objects_header WHERE keyword=@k";
        sel.Parameters.AddWithValue("@k", "SchemaVersion");
        Assert.Equal("1.1", sel.ExecuteScalar()?.ToString());
    }

    // ── value table (UNIQUE constraint) ─────────────────────────────────────

    [Fact]
    public void ValueTable_InsertAndRead_RoundTrip()
    {
        using var conn = OpenSchemaDb();
        var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO _objects_val (id, value) VALUES (1, @v)";
        ins.Parameters.AddWithValue("@v", "200 mm");
        ins.ExecuteNonQuery();

        var sel = conn.CreateCommand();
        sel.CommandText = "SELECT value FROM _objects_val WHERE id=1";
        Assert.Equal("200 mm", sel.ExecuteScalar()?.ToString());
    }

    [Fact]
    public void ValueTable_DuplicateValue_ThrowsUniqueConstraint()
    {
        using var conn = OpenSchemaDb();
        new SQLiteCommand("INSERT INTO _objects_val (id, value) VALUES (1, 'hello')", conn).ExecuteNonQuery();
        // A second row with the same value must violate the UNIQUE constraint.
        var ex = Record.Exception(() =>
            new SQLiteCommand("INSERT INTO _objects_val (id, value) VALUES (2, 'hello')", conn).ExecuteNonQuery());
        Assert.NotNull(ex);
        Assert.IsAssignableFrom<SQLiteException>(ex);
    }

    // ── attr table ───────────────────────────────────────────────────────────

    [Fact]
    public void AttrTable_InsertAndRead_RoundTrip()
    {
        using var conn = OpenSchemaDb();
        var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO _objects_attr (id, name, category, data_type) VALUES (@id, @n, @cat, -1)";
        ins.Parameters.AddWithValue("@id", 100L);
        ins.Parameters.AddWithValue("@n", "Width");
        ins.Parameters.AddWithValue("@cat", "Dimensions");
        ins.ExecuteNonQuery();

        var sel = conn.CreateCommand();
        sel.CommandText = "SELECT name FROM _objects_attr WHERE id=100";
        Assert.Equal("Width", sel.ExecuteScalar()?.ToString());
    }

    // ── EAV table ────────────────────────────────────────────────────────────

    [Fact]
    public void EavTable_InsertAndRead_RoundTrip()
    {
        using var conn = OpenSchemaDb();
        new SQLiteCommand("INSERT INTO _objects_attr (id, name, category, data_type) VALUES (10, 'Height', 'Dims', -1)", conn).ExecuteNonQuery();
        new SQLiteCommand("INSERT INTO _objects_val (id, value) VALUES (5, '3000 mm')", conn).ExecuteNonQuery();

        var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO _objects_eav (entity_id, attribute_id, value_id) VALUES (@eid, @aid, @vid)";
        ins.Parameters.AddWithValue("@eid", 999L);
        ins.Parameters.AddWithValue("@aid", 10L);
        ins.Parameters.AddWithValue("@vid", 5);
        ins.ExecuteNonQuery();

        var sel = conn.CreateCommand();
        sel.CommandText = "SELECT value_id FROM _objects_eav WHERE entity_id=999 AND attribute_id=10";
        Assert.Equal(5L, Convert.ToInt64(sel.ExecuteScalar()));
    }

    [Fact]
    public void EavTable_MultipleParameters_StoredAndReadBack()
    {
        using var conn = OpenSchemaDb();
        new SQLiteCommand("INSERT INTO _objects_val (id, value) VALUES (1, 'A'), (2, 'B')", conn).ExecuteNonQuery();
        new SQLiteCommand("INSERT INTO _objects_attr (id, name, category, data_type) VALUES (1, 'P1', 'X', -1), (2, 'P2', 'X', -1)", conn).ExecuteNonQuery();

        new SQLiteCommand("INSERT INTO _objects_eav (entity_id, attribute_id, value_id) VALUES (7, 1, 1)", conn).ExecuteNonQuery();
        new SQLiteCommand("INSERT INTO _objects_eav (entity_id, attribute_id, value_id) VALUES (7, 2, 2)", conn).ExecuteNonQuery();

        var sel = conn.CreateCommand();
        sel.CommandText = "SELECT COUNT(*) FROM _objects_eav WHERE entity_id=7";
        Assert.Equal(2L, Convert.ToInt64(sel.ExecuteScalar()));
    }

    // ── id table ─────────────────────────────────────────────────────────────

    [Fact]
    public void IdTable_InsertAndRead_RoundTrip()
    {
        using var conn = OpenSchemaDb();
        var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO _objects_id (id, external_id, category, isType, versionguid) VALUES (@id, @ext, @cat, @isType, NULL)";
        ins.Parameters.AddWithValue("@id", 555L);
        ins.Parameters.AddWithValue("@ext", "unique-guid-555");
        ins.Parameters.AddWithValue("@cat", "Walls");
        ins.Parameters.AddWithValue("@isType", 0);
        ins.ExecuteNonQuery();

        var sel = conn.CreateCommand();
        sel.CommandText = "SELECT category FROM _objects_id WHERE id=555";
        Assert.Equal("Walls", sel.ExecuteScalar()?.ToString());
    }

    // ── geom table ───────────────────────────────────────────────────────────

    [Fact]
    public void GeomTable_InsertAndRead_RoundTrip()
    {
        using var conn = OpenSchemaDb();
        var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO _objects_geom (id, BoundingBoxMin, BoundingBoxMax, Location, Location2, Level, Rotation) VALUES (@id, @min, @max, @loc, '', @lev, @rot)";
        ins.Parameters.AddWithValue("@id", 888L);
        ins.Parameters.AddWithValue("@min", "0,0,0");
        ins.Parameters.AddWithValue("@max", "1,2,3");
        ins.Parameters.AddWithValue("@loc", "0.5,1.0,1.5");
        ins.Parameters.AddWithValue("@lev", "Ground Floor");
        ins.Parameters.AddWithValue("@rot", -1.0f);
        ins.ExecuteNonQuery();

        var sel = conn.CreateCommand();
        sel.CommandText = "SELECT BoundingBoxMax, Level FROM _objects_geom WHERE id=888";
        using var reader = sel.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("1,2,3", reader.GetString(0));
        Assert.Equal("Ground Floor", reader.GetString(1));
    }

    // ── parameterized queries prevent SQL injection ──────────────────────────

    [Fact]
    public void ParameterizedInsert_WithSqlInjectionPayload_StoresLiterally()
    {
        using var conn = OpenSchemaDb();
        string payload = "'; DROP TABLE _objects_val; --";

        var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO _objects_header (keyword, value) VALUES (@k, @v)";
        ins.Parameters.AddWithValue("@k", "test");
        ins.Parameters.AddWithValue("@v", payload);
        ins.ExecuteNonQuery();

        // The value must be stored as-is, NOT executed as SQL.
        var sel = conn.CreateCommand();
        sel.CommandText = "SELECT value FROM _objects_header WHERE keyword='test'";
        Assert.Equal(payload, sel.ExecuteScalar()?.ToString());

        // The table must still exist.
        var check = conn.CreateCommand();
        check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='_objects_val'";
        Assert.Equal("_objects_val", check.ExecuteScalar()?.ToString());
    }

    // ── write pragmas don't throw ────────────────────────────────────────────

    [Fact]
    public void WritePragmas_OnOpenConnection_DoNotThrow()
    {
        using var conn = OpenSchemaDb();
        // These are the same pragmas applied in SnapshotMaker; verify they're valid SQL.
        string[] pragmas =
        {
            "PRAGMA synchronous = OFF",
            "PRAGMA journal_mode = MEMORY",
            "PRAGMA temp_store = MEMORY",
            "PRAGMA cache_size = -65536",
            "PRAGMA locking_mode = EXCLUSIVE",
        };
        foreach (string p in pragmas)
            new SQLiteCommand(p, conn).ExecuteNonQuery(); // must not throw
    }
}
