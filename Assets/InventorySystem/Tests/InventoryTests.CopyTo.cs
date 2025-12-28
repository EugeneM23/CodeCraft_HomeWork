using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Inventories
{
    public sealed partial class InventoryTests
    {
        [Test]
        public void CopyTo_Sample_CopiesCorrectly()
        {
            //Arrange:
            var itemDataX = new ItemData
            {
                Name = "X",
                Size = new Vector2Int(2, 2)
            };
            var itemDataY = new ItemData
            {
                Name = "Y",
                Size = new Vector2Int(1, 3)
            };
            var itemDataZ = new ItemData
            {
                Name = "Z",
                Size = new Vector2Int(2, 1)
            };

            var inventory = new Inventory(3, 3, new[]
            {
                new KeyValuePair<ItemData, Vector2Int>(itemDataX, new Vector2Int(0, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemDataY, new Vector2Int(2, 0)),
                new KeyValuePair<ItemData, Vector2Int>(itemDataZ, new Vector2Int(0, 2))
            });

            var x = inventory.First(i => i.itemData.Name == "X");
            var y = inventory.First(i => i.itemData.Name == "Y");
            var z = inventory.First(i => i.itemData.Name == "Z");

            var expected = new[,]
            {
                { x, x, z },
                { x, x, z },
                { y, y, y }
            };
            var actual = new Item[3, 3];

            //Act:
            inventory.CopyTo(actual);

            //Assert:
            Assert.AreEqual(expected, actual);
        }
    }
}