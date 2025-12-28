using System;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
    {
        [TestCase(0, 0)]
        [TestCase(-1, 10)]
        [TestCase(10, -2)]
        [TestCase(-2, -2)]
        [TestCase(0, 10)]
        [TestCase(5, 0)]
        public void WhenCanAddItemOnFreePositionWithInvalidSizeThenException(int width, int height)
        {
            //Arrange:
            var inventory = new Inventory(5, 5);
            var itemData = new ItemData { Name = "Test", Size = new Vector2Int(width, height) };

            //Assert:
            Assert.Catch<ArgumentException>(() => inventory.CanAddItem(itemData));
        }

        [TestCase(0, 0)]
        [TestCase(-1, 10)]
        [TestCase(10, -2)]
        [TestCase(-2, -2)]
        [TestCase(0, 10)]
        [TestCase(5, 0)]
        public void WhenCanAddItemOnSpecifiedPositionWithInvalidSizeThenException(int width, int height)
        {
            //Arrange:
            var inventory = new Inventory(5, 5);
            var itemData = new ItemData { Name = "Test", Size = new Vector2Int(width, height) };

            //Assert:
            Assert.Catch<ArgumentException>(() => inventory.CanAddItem(itemData, Vector2Int.zero));
        }
    }
}