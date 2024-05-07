using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using InfoSupport.StaticCodeAnalyzer.Domain;

namespace DomainTests;

[TestClass]
public class PositionTests
{
    [TestMethod]
    public void Constructor_WithDefaultValues_ShouldInitializeToOne()
    {
        // Arrange & Act
        var position = new Position();

        // Assert
        Assert.AreEqual((ulong)1, position.Line);
        Assert.AreEqual((ulong)1, position.Column);
    }

    [TestMethod]
    public void Constructor_WithCustomValues_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var position = new Position(3, 5);

        // Assert
        Assert.AreEqual((ulong)3, position.Line);
        Assert.AreEqual((ulong)5, position.Column);
    }

    [TestMethod]
    public void Add_ShouldAddPositionsCorrectly()
    {
        // Arrange
        var position1 = new Position(2, 4);
        var position2 = new Position(3, 6);

        // Act
        position1.Add(position2);

        // Assert
        Assert.AreEqual((ulong)5, position1.Line);
        Assert.AreEqual((ulong)10, position1.Column);
    }
}

[TestClass]
public class CodeLocationTests
{
    [TestMethod]
    public void Constructor_WithDefaultValues_ShouldInitializeToPositionOne()
    {
        // Arrange & Act
        var codeLocation = new CodeLocation();

        // Assert
        Assert.AreEqual((ulong)1, codeLocation.Start.Line);
        Assert.AreEqual((ulong)1, codeLocation.Start.Column);
        Assert.AreEqual((ulong)1, codeLocation.End.Line);
        Assert.AreEqual((ulong)1, codeLocation.End.Column);
    }

    [TestMethod]
    public void Constructor_WithCustomValues_ShouldInitializeCorrectly()
    {
        // Arrange
        var start = new Position(2, 4);
        var end = new Position(3, 6);

        // Act
        var codeLocation = new CodeLocation(start, end);

        // Assert
        Assert.AreEqual((ulong)2, codeLocation.Start.Line);
        Assert.AreEqual((ulong)4, codeLocation.Start.Column);
        Assert.AreEqual((ulong)3, codeLocation.End.Line);
        Assert.AreEqual((ulong)6, codeLocation.End.Column);
    }

    [TestMethod]
    public void From_ShouldCreateCopyOfOriginal()
    {
        // Arrange
        var original = new CodeLocation(new Position(2, 4), new Position(3, 6));

        // Act
        var copy = CodeLocation.From(original);

        // Assert
        Assert.AreEqual(original.Start.Line, copy.Start.Line);
        Assert.AreEqual(original.Start.Column, copy.Start.Column);
        Assert.AreEqual(original.End.Line, copy.End.Line);
        Assert.AreEqual(original.End.Column, copy.End.Column);
    }
}

[TestClass]
public class CodeLocationComparatorTests
{
    [TestMethod]
    public void Compare_SameStartDifferentEnd_ShouldCompareBasedOnEnd()
    {
        // Arrange
        var comparator = new CodeLocationComparator();
        var location1 = new CodeLocation(new Position(2, 4), new Position(5, 6));
        var location2 = new CodeLocation(new Position(2, 4), new Position(4, 7));

        // Act
        int result = comparator.Compare(location1, location2);

        // Assert
        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public void Compare_NullValues_ShouldHandleNullsCorrectly()
    {
        // Arrange
        var comparator = new CodeLocationComparator();
        var location1 = new CodeLocation(new Position(2, 4), new Position(5, 6));

        // Act & Assert
        Assert.AreEqual(1, comparator.Compare(location1, null));
        Assert.AreEqual(-1, comparator.Compare(null, location1));
        Assert.AreEqual(0, comparator.Compare(null, null));
    }

    [TestMethod]
    public void Compare_DifferentStarts_ShouldCompareBasedOnStart()
    {
        // Arrange
        var comparator = new CodeLocationComparator();
        var location1 = new CodeLocation(new Position(2, 4), new Position(5, 6));
        var location2 = new CodeLocation(new Position(3, 1), new Position(5, 6));

        // Act
        int result = comparator.Compare(location1, location2);

        // Assert
        Assert.AreEqual(-1, result);
    }
}