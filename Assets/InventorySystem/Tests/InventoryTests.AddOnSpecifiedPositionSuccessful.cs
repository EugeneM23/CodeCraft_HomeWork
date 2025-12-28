using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
    {
        [Test]
        public void AddOnSpecifiedPositionSuccessful_2x2_at_0x0()
        {
            //Arrange:
            var inventory = new Inventory(width: 5, height: 5);
            var itemData = new ItemData
            {
                Name = "A",
                ItemType = ItemType.Weapon,
                Size = new Vector2Int(2, 2),
            };
            var position = new Vector2Int(0, 0);

            Item addedItem = null;
            inventory.OnAdded += (i) => { addedItem = i; };
            int count = inventory.Count;

            //Pre-assert:
            for (int x = position.x; x < position.x + itemData.Size.x; x++)
            for (int y = position.y; y < position.y + itemData.Size.y; y++)
            {
                Assert.IsTrue(inventory.IsFree(x, y));
            }

            //Act:
            bool success = inventory.AddItem(itemData, position);

            //Assert:
            Assert.IsTrue(success);
            Assert.AreEqual(count + 1, inventory.Count);
            Assert.AreEqual(itemData.Name, addedItem.itemData.Name);
            Assert.AreEqual(position, addedItem.GridPosition);

            for (int x = position.x; x < position.x + itemData.Size.x; x++)
            for (int y = position.y; y < position.y + itemData.Size.y; y++)
                Assert.IsFalse(inventory.IsFree(x, y));
        }

        [Test]
        public void AddOnSpecifiedPositionSuccessful_2x2_at_3x3()
        {
            //Arrange:
            var inventory = new Inventory(width: 5, height: 5);
            var itemData = new ItemData
            {
                Name = "A",
                ItemType = ItemType.Weapon,
                Size = new Vector2Int(2, 2),
            };
            var position = new Vector2Int(3, 3);

            Item addedItem = null;
            inventory.OnAdded += (i) => { addedItem = i; };
            int count = inventory.Count;

            //Pre-assert:
            for (int x = position.x; x < position.x + itemData.Size.x; x++)
            for (int y = position.y; y < position.y + itemData.Size.y; y++)
            {
                Assert.IsTrue(inventory.IsFree(x, y));
            }

            //Act:
            bool success = inventory.AddItem(itemData, position);

            //Assert:
            Assert.IsTrue(success);
            Assert.AreEqual(count + 1, inventory.Count);
            Assert.AreEqual(itemData.Name, addedItem.itemData.Name);
            Assert.AreEqual(position, addedItem.GridPosition);

            for (int x = position.x; x < position.x + itemData.Size.x; x++)
            for (int y = position.y; y < position.y + itemData.Size.y; y++)
                Assert.IsFalse(inventory.IsFree(x, y));
        }

        [Test]
        public void AddOnSpecifiedPositionSuccessful_FullItem()
        {
            //Arrange:
            var inventory = new Inventory(width: 5, height: 5);
            var itemData = new ItemData
            {
                Name = "A",
                ItemType = ItemType.Weapon,
                Size = new Vector2Int(5, 5),
            };
            var position = new Vector2Int(0, 0);

            Item addedItem = null;
            inventory.OnAdded += (i) => { addedItem = i; };
            int count = inventory.Count;

            //Pre-assert:
            for (int x = position.x; x < position.x + itemData.Size.x; x++)
            for (int y = position.y; y < position.y + itemData.Size.y; y++)
            {
                Assert.IsTrue(inventory.IsFree(x, y));
            }

            //Act:
            bool success = inventory.AddItem(itemData, position);

            //Assert:
            Assert.IsTrue(success);
            Assert.AreEqual(count + 1, inventory.Count);
            Assert.AreEqual(itemData.Name, addedItem.itemData.Name);
            Assert.AreEqual(position, addedItem.GridPosition);

            for (int x = position.x; x < position.x + itemData.Size.x; x++)
            for (int y = position.y; y < position.y + itemData.Size.y; y++)
                Assert.IsFalse(inventory.IsFree(x, y));
        }

        [Test]
        public void AddOnSpecifiedPositionSuccessful_WithoutName()
        {
            //Arrange:
            var inventory = new Inventory(width: 5, height: 5);
            var itemData = new ItemData
            {
                ItemType = ItemType.Weapon,
                Size = new Vector2Int(5, 5),
            };
            var position = new Vector2Int(0, 0);

            Item addedItem = null;
            inventory.OnAdded += (i) => { addedItem = i; };
            int count = inventory.Count;

            //Pre-assert:
            for (int x = position.x; x < position.x + itemData.Size.x; x++)
            for (int y = position.y; y < position.y + itemData.Size.y; y++)
            {
                Assert.IsTrue(inventory.IsFree(x, y));
            }

            //Act:
            bool success = inventory.AddItem(itemData, position);

            //Assert:
            Assert.IsTrue(success);
            Assert.AreEqual(count + 1, inventory.Count);
            Assert.AreEqual(itemData.Name, addedItem.itemData.Name);
            Assert.AreEqual(position, addedItem.GridPosition);

            for (int x = position.x; x < position.x + itemData.Size.x; x++)
            for (int y = position.y; y < position.y + itemData.Size.y; y++)
                Assert.IsFalse(inventory.IsFree(x, y));
        }
    }
}