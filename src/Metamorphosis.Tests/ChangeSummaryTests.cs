using Metamorphosis.Objects;
using Xunit;

namespace Metamorphosis.Tests;

public class ChangeSummaryTests
{
    [Fact]
    public void ChangeSummary_DefaultCollections_AreInitialized()
    {
        var cs = new ChangeSummary();
        Assert.NotNull(cs.Changes);
        Assert.Empty(cs.Changes);
        Assert.NotNull(cs.ModelSummary);
        Assert.Empty(cs.ModelSummary);
        Assert.NotNull(cs.LevelNames);
        Assert.Empty(cs.LevelNames);
    }

    [Fact]
    public void ChangeSummary_Properties_CanBeAssigned()
    {
        var now = DateTime.UtcNow;
        var cs = new ChangeSummary
        {
            ModelName = "Building.rvt",
            ModelPath = @"C:\Projects\Building.rvt",
            PreviousFile = @"C:\Snapshots\Building_2025.db",
            ComparisonDate = now,
            NumberOfChanges = 3
        };

        Assert.Equal("Building.rvt", cs.ModelName);
        Assert.Equal(@"C:\Projects\Building.rvt", cs.ModelPath);
        Assert.Equal(@"C:\Snapshots\Building_2025.db", cs.PreviousFile);
        Assert.Equal(now, cs.ComparisonDate);
        Assert.Equal(3, cs.NumberOfChanges);
    }

    [Fact]
    public void ChangeSummary_Changes_CanBePopulated()
    {
        var cs = new ChangeSummary();
        cs.Changes.Add(new Change { Category = "Walls", ChangeType = Change.ChangeTypeEnum.Move });
        cs.Changes.Add(new Change { Category = "Doors", ChangeType = Change.ChangeTypeEnum.ParameterChange });

        Assert.Equal(2, cs.Changes.Count);
        Assert.Equal("Walls", cs.Changes[0].Category);
        Assert.Equal(Change.ChangeTypeEnum.ParameterChange, cs.Changes[1].ChangeType);
    }

    [Fact]
    public void ChangeSummary_ModelSummary_CanBePopulated()
    {
        var cs = new ChangeSummary();
        cs.ModelSummary["Walls"] = 42;
        cs.ModelSummary["Doors"] = 7;

        Assert.Equal(42, cs.ModelSummary["Walls"]);
        Assert.Equal(7, cs.ModelSummary["Doors"]);
    }
}
