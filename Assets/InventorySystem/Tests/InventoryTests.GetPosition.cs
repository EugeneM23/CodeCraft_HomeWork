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
        public void GetPositions_ItemD_ReturnsCorrectPositions()
        {
            //Arrange:
            var itemDataD = new ItemData { Name = "D", Size = new Vector2Int(1, 2) };
            var itemDataX = new ItemData { Name = "X", Size = new Vector2Int(3, 2) };

            var inventory = new Inventory(width: 5, height: 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemDataD, new Vector2Int(4, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemDataX, new Vector2Int(1, 2))
            });

            var itemD = inventory.First(i => i.itemData.Name == "D");
            var expected = new[]
            {
                new Vector2Int(4, 0),
                new Vector2Int(4, 1)
            };

            //Act:
            var result = inventory.GetPositions(itemD.ID);

            //Assert:
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void GetPositions_ItemX_ReturnsCorrectPositions()
        {
            //Arrange:
            var itemDataD = new ItemData
            {
                Name = "D",
                Size = new Vector2Int(1, 2)
            };
            var itemDataX = new ItemData
            {
                Name = "X",
                Size = new Vector2Int(3, 2)
            };

            var inventory = new Inventory(width: 5, height: 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemDataD, new Vector2Int(4, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemDataX, new Vector2Int(1, 2))
            });

            var itemX = inventory.First(i => i.itemData.Name == "X");
            var expected = new[]
            {
                new Vector2Int(1, 2),
                new Vector2Int(1, 3),
                new Vector2Int(2, 2),
                new Vector2Int(2, 3),
                new Vector2Int(3, 2),
                new Vector2Int(3, 3)
            };

            //Act:
            var result = inventory.GetPositions(itemX.ID);

            //Assert:
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void WhenGetPositionsAndItemIsNullThenException()
        {
            var inventory = new Inventory(3, 3);
            Assert.Catch<NullReferenceException>(() => inventory.GetPositions(null));
        }

        [Test]
        public void WhenGetPositionsAndItemAbsentThenThrowsException()
        {
            var inventory = new Inventory(3, 3);
            Assert.Catch<KeyNotFoundException>(() => inventory.GetPositions("non-existent-id"));
        }
    }
}