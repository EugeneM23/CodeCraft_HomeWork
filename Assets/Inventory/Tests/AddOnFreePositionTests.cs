using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    [TestFixture]
    public class AddOnFreePositionTests
    {
        private void AssertAddOnFreePosition(
            Inventory inventory,
            Item item,
            Vector2Int expectedPosition)
        {
            // Arrange
            Item addedItem = null;
            Vector2Int addedPosition = Vector2Int.zero;

            inventory.OnAdded += (i, p) =>
            {
                addedItem = i;
                addedPosition = p;
            };

            int oldCount = inventory.Count;

            // Pre-assert
            for (int x = expectedPosition.x; x < expectedPosition.x + item.Size.x; x++)
            for (int y = expectedPosition.y; y < expectedPosition.y + item.Size.y; y++)
            {
                Assert.IsTrue(inventory.IsFree(x, y), $"Cell ({x},{y}) must be free before adding item.");
            }

            // Act
            bool success = inventory.AddItem(item);

            // Assert
            Assert.IsTrue(success, "AddItem() must return true.");
            Assert.AreEqual(item, addedItem, "OnAdded must receive correct item.");
            Assert.AreEqual(expectedPosition, addedPosition, "OnAdded must receive correct position.");

            Assert.AreEqual(oldCount + 1, inventory.Count, "Inventory count must increase by 1.");
            Assert.IsTrue(inventory.Contains(item), "Inventory must contain the added item.");

            // Post-assert
            for (int x = expectedPosition.x; x < expectedPosition.x + item.Size.x; x++)
            for (int y = expectedPosition.y; y < expectedPosition.y + item.Size.y; y++)
            {
                Assert.IsTrue(inventory.IsOccupied(x, y), $"Cell ({x},{y}) must be occupied after adding item.");
            }
        }


        [Test]
        public void AddOnFreePosition_EmptyInventory()
        {
            var inventory = new Inventory(width: 5, height: 5);
            var item = new Item("A", new Vector2Int(2, 2));
            var expected = new Vector2Int(0, 0);

            AssertAddOnFreePosition(inventory, item, expected);
        }

        [Test]
        public void AddOnFreePosition_WithExistingItem_FindsFreeSlot()
        {
            Item existingItem = new Item("X", new Vector2Int(1, 1));

            var inventory = new Inventory(
                width: 5,
                height: 5,
                new KeyValuePair<Item, Vector2Int>(existingItem, new Vector2Int(1, 1))
            );

            Item item = new Item("A", new Vector2Int(3, 3));
            Vector2Int expected = new Vector2Int(2, 0);

            AssertAddOnFreePosition(inventory, item, expected);
        }

        [Test]
        public void AddOnFreePosition_FullItem_FillsInventoryPerfectly()
        {
            var inventory = new Inventory(width: 5, height: 5);
            var item = new Item("A", new Vector2Int(5, 5));
            var expected = new Vector2Int(0, 0);

            AssertAddOnFreePosition(inventory, item, expected);
        }

        [Test]
        public void AddOnFreePosition_ItemWithoutName()
        {
            var inventory = new Inventory(width: 5, height: 5);
            var item = new Item(new Vector2Int(5, 5)); // no name
            var expected = new Vector2Int(0, 0);

            AssertAddOnFreePosition(inventory, item, expected);
        }
    }
}