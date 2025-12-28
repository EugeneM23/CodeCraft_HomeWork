using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
    {
        [Test]
        public void Contains_ItemExists_ReturnsTrue()
        {
            //Arrange:
            var itemData = new ItemData
            {
                Name = "X",
                Size = new Vector2Int(2, 2)
            };
            var inventory = new Inventory(5, 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemData, Vector2Int.zero)
            });
            var item = inventory.First();

            //Act:
            bool result = inventory.Contains(item.ID);

            //Assert:
            Assert.IsTrue(result);
        }

        [Test]
        public void Contains_ItemDoesNotExist_ReturnsFalse()
        {
            //Arrange:
            var itemData = new ItemData
            {
                Name = "B",
                Size = new Vector2Int(2, 2)
            };
            var inventory = new Inventory(5, 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemData, Vector2Int.zero)
            });

            //Act:
            bool result = inventory.Contains("non-existent-id");

            //Assert:
            Assert.IsFalse(result);
        }

        [Test]
        public void Contains_InventoryIsEmpty_ReturnsFalse()
        {
            //Arrange:
            var inventory = new Inventory(5, 5);

            //Act:
            bool result = inventory.Contains("any-id");

            //Assert:
            Assert.IsFalse(result);
        }

        [Test]
        public void Contains_ItemIdIsNull_ReturnsFalse()
        {
            //Arrange:
            var inventory = new Inventory(5, 5);

            //Act:
            bool result = inventory.Contains(null);

            //Assert:
            Assert.IsFalse(result);
        }
    }
}