using Metamorphosis.Objects;
using Newtonsoft.Json;
using Xunit;

namespace Metamorphosis.Tests;

public class SerializationTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string WriteTempJson(string json)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string f in _tempFiles)
            try { File.Delete(f); } catch { }
    }

    // ── round-trip ──────────────────────────────────────────────────────────

    [Fact]
    public void DeSerialize_RoundTrip_PreservesAllFields()
    {
        var summary = new ChangeSummary
        {
            ModelName = "Sample.rvt",
            ModelPath = @"C:\Work\Sample.rvt",
            PreviousFile = @"C:\Snap\previous.db",
            NumberOfChanges = 2,
            LevelNames = new List<string> { "Ground Floor", "Level 1" },
            ModelSummary = new Dictionary<string, int> { ["Walls"] = 10 },
            Changes = new List<Change>
            {
                new Change
                {
                    ElementId = 42,
                    UniqueId = "guid-42",
                    Category = "Walls",
                    ChangeType = Change.ChangeTypeEnum.Move,
                    ChangeDescription = "Location Offset 3in.",
                    Level = "Ground Floor",
                    IsType = false
                }
            }
        };

        string path = WriteTempJson(JsonConvert.SerializeObject(summary));
        ChangeSummary result = ComparisonMaker.DeSerialize(path);

        Assert.Equal("Sample.rvt", result.ModelName);
        Assert.Equal(2, result.NumberOfChanges);
        Assert.Equal(2, result.LevelNames.Count);
        Assert.Single(result.Changes);

        Change c = result.Changes[0];
        Assert.Equal(42, c.ElementId);
        Assert.Equal("guid-42", c.UniqueId);
        Assert.Equal("Walls", c.Category);
        Assert.Equal(Change.ChangeTypeEnum.Move, c.ChangeType);
        Assert.Equal("Location Offset 3in.", c.ChangeDescription);
        Assert.Equal("Ground Floor", c.Level);
        Assert.False(c.IsType);
    }

    [Fact]
    public void DeSerialize_EmptyChangesList_ReturnsZeroChanges()
    {
        var summary = new ChangeSummary { ModelName = "Empty.rvt", NumberOfChanges = 0 };
        string path = WriteTempJson(JsonConvert.SerializeObject(summary));
        var result = ComparisonMaker.DeSerialize(path);
        Assert.Equal("Empty.rvt", result.ModelName);
        Assert.Empty(result.Changes);
    }

    // ── ChangeType enum serialized as string (StringEnumConverter) ───────────

    [Fact]
    public void DeSerialize_ChangeTypeEnum_RoundTripsAsString()
    {
        var change = new Change { Category = "Doors", ChangeType = Change.ChangeTypeEnum.DeletedElement };
        var summary = new ChangeSummary { Changes = new List<Change> { change } };
        string json = JsonConvert.SerializeObject(summary);

        // Verify enum was serialized as a string, not an int
        Assert.Contains("\"DeletedElement\"", json);

        string path = WriteTempJson(json);
        var result = ComparisonMaker.DeSerialize(path);
        Assert.Equal(Change.ChangeTypeEnum.DeletedElement, result.Changes[0].ChangeType);
    }

    // ── security: TypeNameHandling.None ─────────────────────────────────────

    [Fact]
    public void DeSerialize_WithDollarTypeHint_IgnoresTypeHint()
    {
        // $type hints must be silently ignored — TypeNameHandling.None is required to prevent
        // type-confusion attacks where a crafted JSON file causes unexpected code to run.
        string maliciousJson = """
            {
                "$type": "System.Windows.Data.ObjectDataProvider, PresentationFramework",
                "ModelName": "should-be-preserved",
                "NumberOfChanges": 0,
                "Changes": [],
                "LevelNames": [],
                "ModelSummary": {}
            }
            """;

        string path = WriteTempJson(maliciousJson);
        var result = ComparisonMaker.DeSerialize(path);

        // The $type hint must not trigger instantiation of the named type;
        // deserialization must produce a plain ChangeSummary.
        Assert.NotNull(result);
        Assert.IsType<ChangeSummary>(result);
        Assert.Equal("should-be-preserved", result.ModelName);
    }

    // ── security: MaxDepth ───────────────────────────────────────────────────

    [Fact]
    public void DeSerialize_DeeplyNestedJson_ThrowsOrReturnsNull()
    {
        // Build JSON nested 80 levels deep — well beyond MaxDepth=64.
        // The deserializer should throw a JsonReaderException rather than recurse unbounded.
        string open = string.Concat(Enumerable.Repeat("{\"a\":", 80));
        string deepJson = open + "1" + new string('}', 80);

        string path = WriteTempJson(deepJson);
        var ex = Record.Exception(() => ComparisonMaker.DeSerialize(path));

        // Either a JsonException is thrown, or deserialization returns null/incomplete.
        // What must NOT happen: no crash, no infinite recursion, no OOM.
        Assert.True(ex == null || ex is JsonException,
            $"Expected null or JsonException, got: {ex?.GetType().Name}: {ex?.Message}");
    }

    // ── all ChangeTypeEnum values survive a round-trip ──────────────────────

    [Theory]
    [InlineData(Change.ChangeTypeEnum.ParameterChange)]
    [InlineData(Change.ChangeTypeEnum.Move)]
    [InlineData(Change.ChangeTypeEnum.Rotate)]
    [InlineData(Change.ChangeTypeEnum.GeometryChange)]
    [InlineData(Change.ChangeTypeEnum.NewElement)]
    [InlineData(Change.ChangeTypeEnum.DeletedElement)]
    public void DeSerialize_AllChangeTypes_RoundTrip(Change.ChangeTypeEnum changeType)
    {
        var summary = new ChangeSummary
        {
            Changes = new List<Change> { new Change { Category = "Test", ChangeType = changeType } }
        };
        string path = WriteTempJson(JsonConvert.SerializeObject(summary));
        var result = ComparisonMaker.DeSerialize(path);
        Assert.Equal(changeType, result.Changes[0].ChangeType);
    }
}
