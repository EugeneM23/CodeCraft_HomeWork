using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
    {
        [Test]
        public void RemoveFailed_ItemIsNull()
        {
            //Arrange:
            var inventory = new Inventory(5, 5);
            Item removedItem = null;
            inventory.OnRemoved += (i) => { removedItem = i; };

            //Act:
            bool success = inventory.RemoveItem(null);

            //Assert:
            Assert.IsFalse(success);
            Assert.IsNull(removedItem);
        }

        [Test]
        public void RemoveFailed_ItemIsAbsent()
        {
            //Arrange:
            var itemData = new ItemData { Name = "X", Size = new Vector2Int(2, 2) };
            var inventory = new Inventory(width: 5, height: 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(2, 2))
            });
            Item removedItem = null;
            inventory.OnRemoved += (i) => { removedItem = i; };

            //Act:
            bool success = inventory.RemoveItem("non-existent-id");

            //Assert:
            Assert.IsFalse(success);
            Assert.IsNull(removedItem);
        }

        [Test]
        public void RemoveSuccessful_Case1()
        {
            //Arrange:
            var itemData = new ItemData { Name = "X", Size = new Vector2Int(2, 2) };
            var inventory = new Inventory(width: 5, height: 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(2, 2))
            });
            var item = inventory.First();
            var expectedPosition = new Vector2Int(2, 2);
            var expectedSize = new Vector2Int(2, 2);

            Item removedItem = null;
            inventory.OnRemoved += (i) => { removedItem = i; };
            int count = inventory.Count;

            //Pre-assert:
            Assert.IsTrue(inventory.Contains(item.ID));

            for (int x = expectedPosition.x; x < expectedPosition.x + expectedSize.x; x++)
            for (int y = expectedPosition.y; y < expectedPosition.y + expectedSize.y; y++)
            {
                Assert.IsFalse(inventory.IsFree(x, y));
            }

            //Act:
            bool success = inventory.RemoveItem(item.ID);

            //Assert:
            Assert.IsTrue(success);
            Assert.IsNotNull(removedItem);
            Assert.AreEqual(item.ID, removedItem.ID);
            Assert.AreEqual(expectedPosition, removedItem.GridPosition);

            Assert.AreEqual(count - 1, inventory.Count);
            Assert.IsFalse(inventory.Contains(item.ID));

            for (int x = expectedPosition.x; x < expectedPosition.x + expectedSize.x; x++)
            for (int y = expectedPosition.y; y < expectedPosition.y + expectedSize.y; y++)
            {
                Assert.IsTrue(inventory.IsFree(x, y));
            }
        }

        [Test]
        public void RemoveSuccessful_Case2()
        {
            //Arrange:
            var itemDataX = new ItemData { Name = "X", Size = new Vector2Int(3, 2) };
            var itemDataD = new ItemData { Name = "D", Size = new Vector2Int(1, 2) };
            var inventory = new Inventory(width: 5, height: 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemDataX, new Vector2Int(1, 2)),
                new KeyValuePair<ItemData, Vector2Int>(itemDataD, new Vector2Int(4, 0))
            });
            var item = inventory.First(i => i.itemData.Name == "X");
            var expectedPosition = new Vector2Int(1, 2);
            var expectedSize = new Vector2Int(3, 2);

            Item removedItem = null;
            inventory.OnRemoved += (i) => { removedItem = i; };
            int count = inventory.Count;

            //Pre-assert:
            Assert.IsTrue(inventory.Contains(item.ID));

            for (int x = expectedPosition.x; x < expectedPosition.x + expectedSize.x; x++)
            for (int y = expectedPosition.y; y < expectedPosition.y + expectedSize.y; y++)
            {
                Assert.IsFalse(inventory.IsFree(x, y));
            }

            //Act:
            bool success = inventory.RemoveItem(item.ID);

            //Assert:
            Assert.IsTrue(success);
            Assert.IsNotNull(removedItem);
            Assert.AreEqual(item.ID, removedItem.ID);
            Assert.AreEqual(expectedPosition, removedItem.GridPosition);

            Assert.AreEqual(count - 1, inventory.Count);
            Assert.IsFalse(inventory.Contains(item.ID));

            for (int x = expectedPosition.x; x < expectedPosition.x + expectedSize.x; x++)
            for (int y = expectedPosition.y; y < expectedPosition.y + expectedSize.y; y++)
            {
                Assert.IsTrue(inventory.IsFree(x, y));
            }
        }
    }
}