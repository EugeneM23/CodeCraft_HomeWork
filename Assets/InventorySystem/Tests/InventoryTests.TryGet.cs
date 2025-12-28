using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
{
    [Test]
    public void TryGetItem_Position_1_2_ReturnsItemX()
    {
        //Arrange:
        var itemData1 = new ItemData { Name = "D", Size = new Vector2Int(1, 2) };
        var itemData2 = new ItemData { Name = "X", Size = new Vector2Int(3, 2) };

        var inventory = new Inventory(width: 5, height: 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(1, 2)),
            new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(4, 0))
        });

        var item2 = inventory.First(i => i.itemData.Name == "X");

        //Act:
        bool success = inventory.TryGetItem(new Vector2Int(1, 2), out Item actualItem);

        //Assert:
        Assert.IsTrue(success);
        Assert.AreEqual(item2, actualItem);
    }

    [Test]
    public void TryGetItem_Position_1_3_ReturnsItemX()
    {
        //Arrange:
        var itemData1 = new ItemData { Name = "D", Size = new Vector2Int(1, 2) };
        var itemData2 = new ItemData { Name = "X", Size = new Vector2Int(3, 2) };

        var inventory = new Inventory(width: 5, height: 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(1, 2)),
            new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(4, 0))
        });

        var item2 = inventory.First(i => i.itemData.Name == "X");

        //Act:
        bool success = inventory.TryGetItem(new Vector2Int(1, 3), out Item actualItem);

        //Assert:
        Assert.IsTrue(success);
        Assert.AreEqual(item2, actualItem);
    }

    [Test]
    public void TryGetItem_Position_3_3_ReturnsItemX()
    {
        //Arrange:
        var itemData1 = new ItemData { Name = "D", Size = new Vector2Int(1, 2) };
        var itemData2 = new ItemData { Name = "X", Size = new Vector2Int(3, 2) };

        var inventory = new Inventory(width: 5, height: 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(1, 2)),
            new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(4, 0))
        });

        var item2 = inventory.First(i => i.itemData.Name == "X");

        //Act:
        bool success = inventory.TryGetItem(new Vector2Int(3, 3), out Item actualItem);

        //Assert:
        Assert.IsTrue(success);
        Assert.AreEqual(item2, actualItem);
    }

    [Test]
    public void TryGetItem_Position_4_1_ReturnsItemD()
    {
        //Arrange:
        var itemData1 = new ItemData { Name = "D", Size = new Vector2Int(1, 2) };
        var itemData2 = new ItemData { Name = "X", Size = new Vector2Int(3, 2) };

        var inventory = new Inventory(width: 5, height: 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(1, 2)),
            new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(4, 0))
        });

        var item1 = inventory.First(i => i.itemData.Name == "D");

        //Act:
        bool success = inventory.TryGetItem(new Vector2Int(4, 1), out Item actualItem);

        //Assert:
        Assert.IsTrue(success);
        Assert.AreEqual(item1, actualItem);
    }

    [Test]
    public void TryGetItem_Position_0_0_ReturnsNull()
    {
        //Arrange:
        var itemData1 = new ItemData { Name = "D", Size = new Vector2Int(1, 2) };
        var itemData2 = new ItemData { Name = "X", Size = new Vector2Int(3, 2) };

        var inventory = new Inventory(width: 5, height: 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(1, 2)),
            new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(4, 0))
        });

        //Act:
        bool success = inventory.TryGetItem(new Vector2Int(0, 0), out Item actualItem);

        //Assert:
        Assert.IsFalse(success);
        Assert.IsNull(actualItem);
    }

    [TestCase(-1, -1)]
    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    [TestCase(3, 0)]
    [TestCase(0, 3)]
    [TestCase(3, 3)]
    public void WhenTryGetItemOutOfRangeThenFalse(int x, int y)
    {
        //Arrange:
        var inventory = new Inventory(3, 3);

        //Act:
        bool success = inventory.TryGetItem(x, y, out Item actualItem);

        //Assert:
        Assert.IsFalse(success);
        Assert.IsNull(actualItem);
    }
}
}