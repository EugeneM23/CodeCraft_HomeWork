using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
    {
        [Test]
        public void TryGetPositions_ItemD_ReturnsCorrectPositions()
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
            var expectedPositions = new[]
            {
                new Vector2Int(4, 0),
                new Vector2Int(4, 1)
            };
    
            //Act:
            bool success = inventory.TryGetPositions(itemD.ID, out Vector2Int[] actualPositions);
    
            //Assert:
            Assert.IsTrue(success);
            Assert.AreEqual(expectedPositions, actualPositions);
        }
    
        [Test]
        public void TryGetPositions_ItemX_ReturnsCorrectPositions()
        {
            //Arrange:
            var itemDataD = new ItemData { Name = "D", Size = new Vector2Int(1, 2) };
            var itemDataX = new ItemData { Name = "X", Size = new Vector2Int(3, 2) };
    
            var inventory = new Inventory(width: 5, height: 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemDataD, new Vector2Int(4, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemDataX, new Vector2Int(1, 2))
            });
    
            var itemX = inventory.First(i => i.itemData.Name == "X");
            var expectedPositions = new[]
            {
                new Vector2Int(1, 2),
                new Vector2Int(1, 3),
                new Vector2Int(2, 2),
                new Vector2Int(2, 3),
                new Vector2Int(3, 2),
                new Vector2Int(3, 3)
            };
    
            //Act:
            bool success = inventory.TryGetPositions(itemX.ID, out Vector2Int[] actualPositions);
    
            //Assert:
            Assert.IsTrue(success);
            Assert.AreEqual(expectedPositions, actualPositions);
        }
    
        [Test]
        public void TryGetPositions_Null_ReturnsFalse()
        {
            //Arrange:
            var itemDataD = new ItemData { Name = "D", Size = new Vector2Int(1, 2) };
            var itemDataX = new ItemData { Name = "X", Size = new Vector2Int(3, 2) };
    
            var inventory = new Inventory(width: 5, height: 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemDataD, new Vector2Int(4, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemDataX, new Vector2Int(1, 2))
            });
    
            //Act:
            bool success = inventory.TryGetPositions(null, out Vector2Int[] actualPositions);
    
            //Assert:
            Assert.IsFalse(success);
            Assert.IsNull(actualPositions);
        }
    
        [Test]
        public void TryGetPositions_Absent_ReturnsFalse()
        {
            //Arrange:
            var itemDataD = new ItemData { Name = "D", Size = new Vector2Int(1, 2) };
            var itemDataX = new ItemData { Name = "X", Size = new Vector2Int(3, 2) };
    
            var inventory = new Inventory(width: 5, height: 5, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemDataD, new Vector2Int(4, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemDataX, new Vector2Int(1, 2))
            });
    
            //Act:
            bool success = inventory.TryGetPositions("non-existent", out Vector2Int[] actualPositions);
    
            //Assert:
            Assert.IsFalse(success);
            Assert.IsNull(actualPositions);
        }
    }
}