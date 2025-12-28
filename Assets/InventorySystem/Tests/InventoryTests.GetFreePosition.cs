using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
{
    [Test]
    public void FindFreePosition_InventoryIsEmpty_ReturnsTrue()
    {
        //Arrange:
        var inventory = new Inventory(5, 5);
        var size = new Vector2Int(2, 2);
        var expectedPosition = new Vector2Int(0, 0);

        //Act:
        bool success = inventory.FindFreePosition(size, out Vector2Int actualPosition);

        //Assert:
        Assert.IsTrue(success);
        Assert.AreEqual(expectedPosition, actualPosition);
    }

    [Test]
    public void FindFreePosition_FullItem_ReturnsTrue()
    {
        //Arrange:
        var inventory = new Inventory(5, 5);
        var size = new Vector2Int(5, 5);
        var expectedPosition = new Vector2Int(0, 0);

        //Act:
        bool success = inventory.FindFreePosition(size, out Vector2Int actualPosition);

        //Assert:
        Assert.IsTrue(success);
        Assert.AreEqual(expectedPosition, actualPosition);
    }

    [Test]
    public void FindFreePosition_RightOrdering_ReturnsTrue()
    {
        //Arrange:
        var itemData = new ItemData
        {
            Name = "X",
            Size = new Vector2Int(1, 1)
        };
        var inventory = new Inventory(width: 5, height: 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(1, 1))
        });
        var size = new Vector2Int(3, 3);
        var expectedPosition = new Vector2Int(2, 0);

        //Act:
        bool success = inventory.FindFreePosition(size, out Vector2Int actualPosition);

        //Assert:
        Assert.IsTrue(success);
        Assert.AreEqual(expectedPosition, actualPosition);
    }

    [Test]
    public void FindFreePosition_MiddleItem_ReturnsFalse()
    {
        //Arrange:
        var itemData = new ItemData
        {
            Name = "X",
            Size = new Vector2Int(3, 3)
        };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(1, 1))
        });
        var size = new Vector2Int(2, 2);
        var expectedPosition = new Vector2Int();

        //Act:
        bool success = inventory.FindFreePosition(size, out Vector2Int actualPosition);

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(expectedPosition, actualPosition);
    }

    [Test]
    public void FindFreePosition_TopRightCornerSpace_ReturnsTrue()
    {
        //Arrange:
        var itemDataX = new ItemData
        {
            Name = "X",
            Size = new Vector2Int(3, 5)
        };
        var itemDataY = new ItemData
        {
            Name = "Y",
            Size = new Vector2Int(2, 3)
        };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemDataX, new Vector2Int(0, 0)),
            new KeyValuePair<ItemData, Vector2Int>(itemDataY, new Vector2Int(3, 0))
        });
        var size = new Vector2Int(2, 2);
        var expectedPosition = new Vector2Int(3, 3);

        //Act:
        bool success = inventory.FindFreePosition(size, out Vector2Int actualPosition);

        //Assert:
        Assert.IsTrue(success);
        Assert.AreEqual(expectedPosition, actualPosition);
    }

    [Test]
    public void FindFreePosition_ItemSizeBiggerThanInventory_ReturnsFalse()
    {
        //Arrange:
        var inventory = new Inventory(5, 5);
        var size = new Vector2Int(6, 6);
        var expectedPosition = new Vector2Int(0, 0);

        //Act:
        bool success = inventory.FindFreePosition(size, out Vector2Int actualPosition);

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(expectedPosition, actualPosition);
    }

    [TestCase(0, 0)]
    [TestCase(-1, 10)]
    [TestCase(10, -2)]
    [TestCase(-2, -2)]
    [TestCase(0, 10)]
    [TestCase(5, 0)]
    public void WhenGetFreePositionWithInvalidSizeThenException(int width, int height)
    {
        //Arrange:
        var inventory = new Inventory(5, 5);

        //Assert:
        Assert.Catch<ArgumentOutOfRangeException>(() =>
            inventory.FindFreePosition(new Vector2Int(width, height), out _));
    }
}
}