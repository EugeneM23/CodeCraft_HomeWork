using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
    {
        [Test]
        public void CanAddOnFreePosition_EmptyInventory_ReturnsTrue()
        {
            Debug.Log("CanAddOnFreePosition_FreeSlot");

            //Arrange:
            var inventory = new Inventory(width: 5, height: 5);
            var itemData = new ItemData()
            {
                Name = "A",
                Size = new Vector2Int(2, 2)
            };

            //Act:
            bool result = inventory.CanAddItem(itemData);

            //Assert:
            Assert.IsTrue(result);
        }

        [Test]
        public void CanAddOnFreePosition_FreeSlot_ReturnsTrue()
        {
            Debug.Log("CanAddOnFreePosition_FreeSlot");

            //Arrange:
            var existingItemData = new ItemData()
            {
                Name = "X",
                Size = new Vector2Int(1, 1)
            };
            var inventory = new Inventory(width: 5, height: 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(existingItemData, new Vector2Int(3, 3))
            });

            var newItemData = new ItemData()
            {
                Name = "A",
                Size = new Vector2Int(2, 2)
            };

            //Act:
            bool result = inventory.CanAddItem(newItemData);

            //Assert:
            Assert.IsTrue(result);
        }

        [Test]
        public void CanAddOnFreePosition_Intersects_ReturnsFalse()
        {
            //Arrange:
            var existingItemData = new ItemData()
            {
                Name = "X",
                Size = new Vector2Int(2, 2)
            };
            var inventory = new Inventory(width: 5, height: 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(existingItemData, new Vector2Int(2, 2))
            });

            var newItemData = new ItemData()
            {
                Name = "A",
                Size = new Vector2Int(3, 3)
            };

            //Act:
            bool result = inventory.CanAddItem(newItemData);

            //Assert:
            Assert.IsFalse(result);
        }

        [Test]
        public void CanAddOnFreePosition_ItemIsNull_ReturnsFalse()
        {
        
            //Arrange:
            var inventory = new Inventory(width: 5, height: 5);
            ItemData itemData = default;
        
            //Act:
            bool result = inventory.CanAddItem(itemData);
        
            //Assert:
            Assert.IsFalse(result);
        }
    }
}