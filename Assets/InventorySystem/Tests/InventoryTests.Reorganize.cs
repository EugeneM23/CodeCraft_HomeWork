using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
{
    [Test]
    public void ReorganizeSpace_Simple()
    {
        //Arrange:
        var itemData1 = new ItemData { Name = "1", Size = new Vector2Int(1, 1) };
        var itemData2 = new ItemData { Name = "2", Size = new Vector2Int(1, 1) };
        var itemData3 = new ItemData { Name = "3", Size = new Vector2Int(1, 1) };
        var itemData4 = new ItemData { Name = "4", Size = new Vector2Int(1, 1) };
        var itemData5 = new ItemData { Name = "5", Size = new Vector2Int(2, 2) };

        var inventory = new Inventory(4, 4, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(0, 0)),
            new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(3, 3)),
            new KeyValuePair<ItemData, Vector2Int>(itemData3, new Vector2Int(0, 3)),
            new KeyValuePair<ItemData, Vector2Int>(itemData4, new Vector2Int(3, 0)),
            new KeyValuePair<ItemData, Vector2Int>(itemData5, new Vector2Int(1, 1))
        });

        //Act:
        inventory.Reorganize();

        //Assert:
        Item[,] actual = new Item[inventory.Width, inventory.Height];
        inventory.CopyTo(actual);

        var item5 = inventory.First(i => i.itemData.Name == "5");
        var item1 = inventory.First(i => i.itemData.Name == "1");
        var item3 = inventory.First(i => i.itemData.Name == "3");
        var item2 = inventory.First(i => i.itemData.Name == "2");
        var item4 = inventory.First(i => i.itemData.Name == "4");

        var expected = new[,]
        {
            { item5, item5, null, null },
            { item5, item5, null, null },
            { item1, item3, null, null },
            { item2, item4, null, null }
        };

        Assert.AreEqual(expected, actual);
    }

    [Test]
    public void ReorganizeSpace_Full()
    {
        //Arrange:
        var itemData1 = new ItemData { Name = "1", Size = new Vector2Int(1, 2) };
        var itemData2 = new ItemData { Name = "2", Size = new Vector2Int(2, 2) };
        var itemData3 = new ItemData { Name = "3", Size = new Vector2Int(1, 4) };
        var itemData4 = new ItemData { Name = "4", Size = new Vector2Int(3, 2) };

        var inventory = new Inventory(4, 4, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(0, 0)),
            new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(1, 0)),
            new KeyValuePair<ItemData, Vector2Int>(itemData3, new Vector2Int(3, 0)),
            new KeyValuePair<ItemData, Vector2Int>(itemData4, new Vector2Int(0, 2))
        });

        //Act:
        inventory.Reorganize();

        //Assert:
        Item[,] actual = new Item[inventory.Width, inventory.Height];
        inventory.CopyTo(actual);

        var item4 = inventory.First(i => i.itemData.Name == "4");
        var item2 = inventory.First(i => i.itemData.Name == "2");
        var item1 = inventory.First(i => i.itemData.Name == "1");
        var item3 = inventory.First(i => i.itemData.Name == "3");

        var expected = new[,]
        {
            { item4, item4, item2, item2 },
            { item4, item4, item2, item2 },
            { item4, item4, item1, item1 },
            { item3, item3, item3, item3 }
        };

        Assert.AreEqual(expected, actual);
    }

    [Test]
    public void ReorganizeSpace_Medium()
    {
        //Arrange:
        var itemData1 = new ItemData { Name = "1", Size = new Vector2Int(1, 1) };
        var itemData2 = new ItemData { Name = "2", Size = new Vector2Int(2, 3) };
        var itemData3 = new ItemData { Name = "3", Size = new Vector2Int(2, 1) };
        var itemData4 = new ItemData { Name = "4", Size = new Vector2Int(2, 1) };
        var itemData5 = new ItemData { Name = "5", Size = new Vector2Int(2, 2) };
        var itemData6 = new ItemData { Name = "6", Size = new Vector2Int(2, 1) };
        var itemData7 = new ItemData { Name = "7", Size = new Vector2Int(1, 4) };

        var inventory = new Inventory(5, 5, new[]
        {
            new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(0, 0)),
            new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(0, 2)),
            new KeyValuePair<ItemData, Vector2Int>(itemData3, new Vector2Int(3, 4)),
            new KeyValuePair<ItemData, Vector2Int>(itemData4, new Vector2Int(2, 3)),
            new KeyValuePair<ItemData, Vector2Int>(itemData5, new Vector2Int(2, 1)),
            new KeyValuePair<ItemData, Vector2Int>(itemData6, new Vector2Int(2, 0)),
            new KeyValuePair<ItemData, Vector2Int>(itemData7, new Vector2Int(4, 0))
        });

        //Act:
        inventory.Reorganize();

        //Assert:
        Item[,] actual = new Item[inventory.Width, inventory.Height];
        inventory.CopyTo(actual);

        var item2 = inventory.First(i => i.itemData.Name == "2");
        var item4 = inventory.First(i => i.itemData.Name == "4");
        var item1 = inventory.First(i => i.itemData.Name == "1");
        var item5 = inventory.First(i => i.itemData.Name == "5");
        var item3 = inventory.First(i => i.itemData.Name == "3");
        var item6 = inventory.First(i => i.itemData.Name == "6");
        var item7 = inventory.First(i => i.itemData.Name == "7");

        var expected = new[,]
        {
            { item2, item2, item2, item4, item1 },
            { item2, item2, item2, item4, null },
            { item5, item5, item3, item6, null },
            { item5, item5, item3, item6, null },
            { item7, item7, item7, item7, null }
        };

        Assert.AreEqual(expected, actual);
    }
}
}