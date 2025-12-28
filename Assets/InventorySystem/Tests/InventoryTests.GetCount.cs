using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
    {
        [Test]
        public void GetItemCount_NameX_Returns3()
        {
            //Arrange:
            var itemData1 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData2 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData3 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData4 = new ItemData { Name = "", Size = new Vector2Int(1, 1) };
            var itemData5 = new ItemData { Name = null, Size = new Vector2Int(1, 1) };

            var inventory = new Inventory(5, 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(0, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(0, 1)),
                new KeyValuePair<ItemData, Vector2Int>(itemData3, new Vector2Int(1, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemData4, new Vector2Int(1, 1)),
                new KeyValuePair<ItemData, Vector2Int>(itemData5, new Vector2Int(2, 1))
            });

            //Act:
            int result = inventory.GetItemCount("X");

            //Assert:
            Assert.AreEqual(3, result);
        }

        [Test]
        public void GetItemCount_EmptyName_Returns1()
        {
            //Arrange:
            var itemData1 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData2 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData3 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData4 = new ItemData { Name = "", Size = new Vector2Int(1, 1) };
            var itemData5 = new ItemData { Name = null, Size = new Vector2Int(1, 1) };

            var inventory = new Inventory(5, 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(0, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(0, 1)),
                new KeyValuePair<ItemData, Vector2Int>(itemData3, new Vector2Int(1, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemData4, new Vector2Int(1, 1)),
                new KeyValuePair<ItemData, Vector2Int>(itemData5, new Vector2Int(2, 1))
            });

            //Act:
            int result = inventory.GetItemCount("");

            //Assert:
            Assert.AreEqual(1, result);
        }

        [Test]
        public void GetItemCount_NullName_Returns1()
        {
            //Arrange:
            var itemData1 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData2 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData3 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData4 = new ItemData { Name = "", Size = new Vector2Int(1, 1) };
            var itemData5 = new ItemData { Name = null, Size = new Vector2Int(1, 1) };

            var inventory = new Inventory(5, 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(0, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(0, 1)),
                new KeyValuePair<ItemData, Vector2Int>(itemData3, new Vector2Int(1, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemData4, new Vector2Int(1, 1)),
                new KeyValuePair<ItemData, Vector2Int>(itemData5, new Vector2Int(2, 1))
            });

            //Act:
            int result = inventory.GetItemCount(null);

            //Assert:
            Assert.AreEqual(1, result);
        }

        [Test]
        public void GetItemCount_AbsentName_Returns0()
        {
            //Arrange:
            var itemData1 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData2 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData3 = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
            var itemData4 = new ItemData { Name = "", Size = new Vector2Int(1, 1) };
            var itemData5 = new ItemData { Name = null, Size = new Vector2Int(1, 1) };

            var inventory = new Inventory(5, 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemData1, new Vector2Int(0, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemData2, new Vector2Int(0, 1)),
                new KeyValuePair<ItemData, Vector2Int>(itemData3, new Vector2Int(1, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemData4, new Vector2Int(1, 1)),
                new KeyValuePair<ItemData, Vector2Int>(itemData5, new Vector2Int(2, 1))
            });

            //Act:
            int result = inventory.GetItemCount("F");

            //Assert:
            Assert.AreEqual(0, result);
        }
    }
}