using Metamorphosis.Objects;
using Xunit;

namespace Metamorphosis.Tests;

public class ChangeModelTests
{
    [Fact]
    public void Change_DefaultValues_AreCorrect()
    {
        var c = new Change();
        Assert.Equal(string.Empty, c.Level);
        Assert.Equal(string.Empty, c.ChangeDescription);
        Assert.False(c.IsType);
        Assert.Null(c.MoveDescription);
        Assert.Null(c.RotationDescription);
        Assert.Null(c.BoundingBoxDescription);
        Assert.Null(c.UniqueId);
        Assert.Null(c.Category);
    }

    [Theory]
    [InlineData("Walls",  Change.ChangeTypeEnum.Move,             "Walls: Move")]
    [InlineData("Doors",  Change.ChangeTypeEnum.ParameterChange,  "Doors: ParameterChange")]
    [InlineData("(none)", Change.ChangeTypeEnum.NewElement,        "(none): NewElement")]
    [InlineData("Floors", Change.ChangeTypeEnum.DeletedElement,    "Floors: DeletedElement")]
    [InlineData("Beams",  Change.ChangeTypeEnum.GeometryChange,    "Beams: GeometryChange")]
    [InlineData("Cols",   Change.ChangeTypeEnum.Rotate,            "Cols: Rotate")]
    public void Change_ToString_ReturnsCategoryAndType(
        string category, Change.ChangeTypeEnum type, string expected)
    {
        var c = new Change { Category = category, ChangeType = type };
        Assert.Equal(expected, c.ToString());
    }

    [Fact]
    public void Change_IsType_CanBeSetTrue()
    {
        var c = new Change { IsType = true };
        Assert.True(c.IsType);
    }

    [Fact]
    public void Change_AllProperties_CanBeAssigned()
    {
        var c = new Change
        {
            ElementId = 12345L,
            UniqueId = "abc-123",
            Category = "Walls",
            ChangeType = Change.ChangeTypeEnum.ParameterChange,
            Level = "Level 1",
            BoundingBoxDescription = "0,0,0,1,2,3",
            ChangeDescription = "Width: 200 to 300",
            IsType = false,
            MoveDescription = "0,0,0,1,0,0",
            RotationDescription = "0,0,0,0,0,1,0.5"
        };

        Assert.Equal(12345L, c.ElementId);
        Assert.Equal("abc-123", c.UniqueId);
        Assert.Equal("Walls", c.Category);
        Assert.Equal(Change.ChangeTypeEnum.ParameterChange, c.ChangeType);
        Assert.Equal("Level 1", c.Level);
        Assert.Equal("0,0,0,1,2,3", c.BoundingBoxDescription);
        Assert.Equal("Width: 200 to 300", c.ChangeDescription);
        Assert.False(c.IsType);
        Assert.Equal("0,0,0,1,0,0", c.MoveDescription);
        Assert.Equal("0,0,0,0,0,1,0.5", c.RotationDescription);
    }
}
