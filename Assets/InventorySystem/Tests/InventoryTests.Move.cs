using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
{
    [Test]
    public void WhenMoveNullItemThenException()
    {
        //Arrange:
        var inventory = new Inventory(5, 5);

        //Assert:
        Assert.Catch<ArgumentNullException>(() => inventory.MoveItem(null, new Vector2Int(1, 1)));
    }

    [Test]
    public void MoveItemFailed_AbsentItem()
    {
        //Arrange:
        var inventory = new Inventory(5, 5);
        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem("non-existent-id", new Vector2Int(2, 2));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }

    [Test]
    public void MoveItemFailed_IntersectsWithAnother()
    {
        //Arrange:
        var itemDataX = new ItemData { Name = "X", Size = new Vector2Int(2, 2) };
        var itemDataZ = new ItemData { Name = "Z", Size = new Vector2Int(2, 1) };

        var inventory = new Inventory(3, 3, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemDataX, new Vector2Int(0, 0)),
            new KeyValuePair<ItemData, Vector2Int>(itemDataZ, new Vector2Int(0, 2))
        });

        var z = inventory.First(i => i.itemData.Name == "Z");

        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem(z.ID, new Vector2Int(1, 1));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }

    [Test]
    public void MoveItemFailed_OutOfRangePosition_Minus1_Minus1()
    {
        //Arrange:
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(1, 1) };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(0, 0))
        });
        var item = inventory.First();

        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem(item.ID, new Vector2Int(-1, -1));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }

    [Test]
    public void MoveItemFailed_OutOfRangePosition_Minus1_1()
    {
        //Arrange:
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(1, 1) };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(0, 0))
        });
        var item = inventory.First();

        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem(item.ID, new Vector2Int(-1, 1));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }

    [Test]
    public void MoveItemFailed_OutOfRangePosition_1_Minus1()
    {
        //Arrange:
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(1, 1) };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(0, 0))
        });
        var item = inventory.First();

        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem(item.ID, new Vector2Int(1, -1));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }

    [Test]
    public void MoveItemFailed_OutOfRangePosition_5_5()
    {
        //Arrange:
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(1, 1) };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(0, 0))
        });
        var item = inventory.First();

        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem(item.ID, new Vector2Int(5, 5));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }

    [Test]
    public void MoveItemFailed_OutOfRangePosition_5_0()
    {
        //Arrange:
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(1, 1) };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(0, 0))
        });
        var item = inventory.First();

        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem(item.ID, new Vector2Int(5, 0));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }

    [Test]
    public void MoveItemFailed_OutOfRangePosition_0_5()
    {
        //Arrange:
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(1, 1) };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(0, 0))
        });
        var item = inventory.First();

        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem(item.ID, new Vector2Int(0, 5));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }

    [Test]
    public void MoveItemFailed_NearBounds_3_3()
    {
        //Arrange:
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(3, 3) };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(0, 0))
        });
        var item = inventory.First();

        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem(item.ID, new Vector2Int(3, 3));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }

    [Test]
    public void MoveItemFailed_NearBounds_4_2()
    {
        //Arrange:
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(3, 3) };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(0, 0))
        });
        var item = inventory.First();

        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem(item.ID, new Vector2Int(4, 2));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }

    [Test]
    public void MoveItemFailed_NearBounds_3_4()
    {
        //Arrange:
        var itemData = new ItemData { Name = "Test", Size = new Vector2Int(3, 3) };
        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(0, 0))
        });
        var item = inventory.First();

        Item movedItem = default;
        Vector2Int movedPosition = Vector2Int.zero;
        inventory.OnMoved += (i, p) =>
        {
            movedItem = i;
            movedPosition = p;
        };

        //Act:
        bool success = inventory.MoveItem(item.ID, new Vector2Int(3, 4));

        //Assert:
        Assert.IsFalse(success);
        Assert.AreEqual(default, movedItem);
        Assert.AreEqual(Vector2Int.zero, movedPosition);
    }
}
}