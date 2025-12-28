using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
{
    [Test]
    public void AddOnFreePositionFailed_Intersects()
    {
        //Arrange:
        var existingItemData = new ItemData { Name = "X", Size = new Vector2Int(2, 2) };
        var inventory = new Inventory(width: 5, height: 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(existingItemData, new Vector2Int(2, 2))
        });
        var newItemData = new ItemData { Name = "A", Size = new Vector2Int(3, 3) };

        Item addedItem = null;
        inventory.OnAdded += (i) => { addedItem = i; };

        //Act:
        bool success = inventory.AddItem(newItemData);

        //Assert:
        Assert.IsFalse(success);
        Assert.IsNull(addedItem);
    }

    [Test]
    public void AddOnFreePositionFailed_ItemIsNull()
    {
        //Arrange:
        var inventory = new Inventory(width: 5, height: 5);
        Item addedItem = null;
        inventory.OnAdded += (i) => { addedItem = i; };

        //Act:
        bool success = inventory.AddItem(default);

        //Assert:
        Assert.IsFalse(success);
        Assert.IsNull(addedItem);
    }

    [TestCase(0, 0)]
    [TestCase(-1, 10)]
    [TestCase(10, -2)]
    [TestCase(-2, -2)]
    [TestCase(0, 10)]
    [TestCase(5, 0)]
    public void WhenAddOnFreePositionItemWithInvalidSizeThenException(int width, int height)
    {
        //Arrange:
        var inventory = new Inventory(5, 5);
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(width, height) };

        //Assert:
        Assert.Catch<ArgumentException>(() => inventory.AddItem(itemData));
    }

    [TestCase(0, 0)]
    [TestCase(-1, 10)]
    [TestCase(10, -2)]
    [TestCase(-2, -2)]
    [TestCase(0, 10)]
    [TestCase(5, 0)]
    public void WhenAddOnSpecifiedPositionItemWithInvalidSizeThenException(int width, int height)
    {
        //Arrange:
        var inventory = new Inventory(5, 5);
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(width, height) };

        //Assert:
        Assert.Catch<ArgumentException>(() => inventory.AddItem(itemData, Vector2Int.zero));
    }
}
}