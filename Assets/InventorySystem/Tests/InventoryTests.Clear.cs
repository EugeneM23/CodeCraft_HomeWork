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
        public void Clear()
        {
            //Arrange:
            var tests = new InventoryTests();
            var itemDataD = new ItemData()
            {
                Name = "D",
                Size = new Vector2Int(1, 2)
            };

            var itemDataX = new ItemData()
            {
                Name = "X",
                Size = new Vector2Int(3, 2)
            };
            var inventory = new Inventory(5, 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemDataD, new Vector2Int(4, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemDataX, new Vector2Int(1, 2))
            });

            bool wasEvent = false;
            inventory.OnCleared += () => wasEvent = true;

            //Act:
            inventory.Clear();

            //Assert:
            Assert.AreEqual(0, inventory.Count);
            Assert.IsTrue(wasEvent);

            Assert.AreEqual(Array.Empty<Item>(), inventory.ToArray());

            for (int x = 0; x < inventory.Width; x++)
            for (int y = 0; y < inventory.Height; y++)
            {
                Assert.IsTrue(inventory.IsFree(x, y));
            }
        }

        [Test]
        public void WhenClearEmptyInventoryThenEventNotRisen()
        {
            //Arrange:
            var inventory = new Inventory(5, 5);

            bool wasEvent = false;
            inventory.OnCleared += () => wasEvent = true;

            //Act:
            inventory.Clear();

            //Assert:
            Assert.IsFalse(wasEvent);
        }
    }
}