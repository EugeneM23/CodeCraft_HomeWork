// using System;
// using System.Collections.Generic;
// using NUnit.Framework;
// using UnityEngine;
//
// namespace Inventories
// {
//     public sealed partial class InventoryTests
//     {
//         [TestCase(5, 10)]
//         [TestCase(3, 2)]
//         [TestCase(1, 100)]
//         [TestCase(255, 1)]
//         public void Instantiate(int width, int height)
//         {
//             //Act:
//             var inventory = new Inventory(width, height);
//
//             //Assert:
//             Assert.AreEqual(width, inventory.Width);
//             Assert.AreEqual(height, inventory.Height);
//             Assert.AreEqual(0, inventory.Count);
//
//             for (int x = 0; x < width; x++)
//             for (int y = 0; y < height; y++)
//             {
//                 Assert.IsTrue(inventory.IsFree(x, y));
//             }
//         }
//
//         [Test]
//         public void WhenInstantiateWithNullItemsThenException()
//         {
//             //Assert:
//             Assert.Catch<ArgumentNullException>(() =>
//             {
//                 var _ = new Inventory(5, 3, (IEnumerable<KeyValuePair<ItemData, Vector2Int>>)null);
//             });
//
//             Assert.Catch<ArgumentNullException>(() =>
//             {
//                 var _ = new Inventory(5, 3, (KeyValuePair<ItemData, Vector2Int>[])null);
//             });
//
//             Assert.Catch<ArgumentNullException>(() =>
//             {
//                 var _ = new Inventory(5, 3, (IEnumerable<ItemData>)null);
//             });
//
//             Assert.Catch<ArgumentNullException>(() =>
//             {
//                 var _ = new Inventory(5, 3, (ItemData[])null);
//             });
//         }
//
//         [TestCase(-1, 10)]
//         [TestCase(2, -1)]
//         [TestCase(-10, -100)]
//         [TestCase(0, 0)]
//         [TestCase(10, 0)]
//         [TestCase(0, 10)]
//         public void WhenInstantiateWithInvalidSizeThenException(int width, int height)
//         {
//             //Assert:
//             Assert.Catch<ArgumentOutOfRangeException>(() =>
//             {
//                 var _ = new Inventory(width, height);
//             });
//         }
//
//         [Test]
//         public void InstantiateWithItems_FullItem()
//         {
//             //Arrange:
//             var itemData = new ItemData
//             {
//                 Name = "A",
//                 Size = new Vector2Int(10, 10)
//             };
//             var items = new[]
//             {
//                 new KeyValuePair<ItemData, Vector2Int>(itemData, new Vector2Int(0, 0))
//             };
//
//             //Act:
//             var inventory = new Inventory(10, 10, items);
//
//             //Assert:
//             Assert.AreEqual(10, inventory.Width);
//             Assert.AreEqual(10, inventory.Height);
//             Assert.AreEqual(items.Length, inventory.Count);
//         }
//
//         [Test]
//         public void InstantiateWithItems_HalfFilling()
//         {
//             //Arrange:
//             var items = new[]
//             {
//                 new KeyValuePair<ItemData, Vector2Int>(new ItemData { Name = "A", Size = new Vector2Int(1, 2) },
//                     new Vector2Int(0, 0)),
//                 new KeyValuePair<ItemData, Vector2Int>(new ItemData { Name = "B", Size = new Vector2Int(1, 1) },
//                     new Vector2Int(0, 3)),
//                 new KeyValuePair<ItemData, Vector2Int>(new ItemData { Name = "C", Size = new Vector2Int(3, 2) },
//                     new Vector2Int(1, 2)),
//                 new KeyValuePair<ItemData, Vector2Int>(new ItemData { Name = "D", Size = new Vector2Int(2, 1) },
//                     new Vector2Int(2, 0))
//             };
//
//             //Act:
//             var inventory = new Inventory(4, 4, items);
//
//             //Assert:
//             Assert.AreEqual(4, inventory.Width);
//             Assert.AreEqual(4, inventory.Height);
//             Assert.AreEqual(items.Length, inventory.Count);
//         }
//
//         [Test]
//         public void InstantiateWithItems_FullDense()
//         {
//             //Arrange:
//             var items = new[]
//             {
//                 new KeyValuePair<ItemData, Vector2Int>(new ItemData { Name = "A", Size = new Vector2Int(2, 2) },
//                     new Vector2Int(0, 0)),
//                 new KeyValuePair<ItemData, Vector2Int>(new ItemData { Name = "B", Size = new Vector2Int(2, 2) },
//                     new Vector2Int(2, 0)),
//                 new KeyValuePair<ItemData, Vector2Int>(new ItemData { Name = "C", Size = new Vector2Int(2, 2) },
//                     new Vector2Int(0, 2)),
//                 new KeyValuePair<ItemData, Vector2Int>(new ItemData { Name = "D", Size = new Vector2Int(2, 2) },
//                     new Vector2Int(2, 2))
//             };
//
//             //Act:
//             var inventory = new Inventory(4, 4, items);
//
//             //Assert:
//             Assert.AreEqual(4, inventory.Width);
//             Assert.AreEqual(4, inventory.Height);
//             Assert.AreEqual(items.Length, inventory.Count);
//         }
//
//         [Test]
//         public void WhenAddNullItemOnSpecifiedPositionThenFalse()
//         {
//             //Arrange:
//             var inventory = new Inventory(5, 5);
//
//             Item addedItem = null;
//
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(default, 0, 0);
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.AreEqual(0, inventory.Count);
//             Assert.IsNull(addedItem);
//             Assert.IsTrue(inventory.IsFree(0, 0));
//         }
//
//         [Test]
//         public void WhenAddItemOutOfRangePosition_Minus1_0_ThenFalse()
//         {
//             //Arrange:
//             var inventory = new Inventory(5, 5);
//             var itemData = new ItemData { Name = "A", Size = new Vector2Int(1, 1) };
//             Item addedItem = null;
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(itemData, new Vector2Int(-1, 0));
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.IsNull(addedItem);
//         }
//
//         [Test]
//         public void WhenAddItemOutOfRangePosition_Minus1_Minus1_ThenFalse()
//         {
//             //Arrange:
//             var inventory = new Inventory(5, 5);
//             var itemData = new ItemData { Name = "A", Size = new Vector2Int(1, 1) };
//             Item addedItem = null;
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(itemData, new Vector2Int(-1, -1));
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.IsNull(addedItem);
//         }
//
//         [Test]
//         public void WhenAddItemOutOfRangePosition_0_Minus1_ThenFalse()
//         {
//             //Arrange:
//             var inventory = new Inventory(5, 5);
//             var itemData = new ItemData { Name = "A", Size = new Vector2Int(1, 1) };
//             Item addedItem = null;
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(itemData, new Vector2Int(0, -1));
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.IsNull(addedItem);
//         }
//
//         [Test]
//         public void WhenAddItemOutOfRangePosition_5_5_ThenFalse()
//         {
//             //Arrange:
//             var inventory = new Inventory(5, 5);
//             var itemData = new ItemData { Name = "A", Size = new Vector2Int(1, 1) };
//             Item addedItem = null;
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(itemData, new Vector2Int(5, 5));
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.IsNull(addedItem);
//         }
//
//         [Test]
//         public void WhenAddItemOutOfRangePosition_5_0_ThenFalse()
//         {
//             //Arrange:
//             var inventory = new Inventory(5, 5);
//             var itemData = new ItemData { Name = "A", Size = new Vector2Int(1, 1) };
//             Item addedItem = null;
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(itemData, new Vector2Int(5, 0));
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.IsNull(addedItem);
//         }
//
//         [Test]
//         public void WhenAddItemOutOfRangePosition_0_5_ThenFalse()
//         {
//             //Arrange:
//             var inventory = new Inventory(5, 5);
//             var itemData = new ItemData { Name = "A", Size = new Vector2Int(1, 1) };
//             Item addedItem = null;
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(itemData, new Vector2Int(0, 5));
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.IsNull(addedItem);
//         }
//
//         [Test]
//         public void WhenAddItemOutOfRangePosition_0_4_WithSize1x2_ThenFalse()
//         {
//             //Arrange:
//             var inventory = new Inventory(5, 5);
//             var itemData = new ItemData { Name = "A", Size = new Vector2Int(1, 2) };
//             Item addedItem = null;
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(itemData, new Vector2Int(0, 4));
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.IsNull(addedItem);
//         }
//
//         [Test]
//         public void WhenAddItemOutOfRangePosition_3_3_WithSize2x3_ThenFalse()
//         {
//             //Arrange:
//             var inventory = new Inventory(5, 5);
//             var itemData = new ItemData { Name = "", Size = new Vector2Int(2, 3) };
//             Item addedItem = null;
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(itemData, new Vector2Int(3, 3));
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.IsNull(addedItem);
//         }
//
//         [Test]
//         public void WhenAddItemOnSpecifiedPositionThatIntersects_SideIntersect_ThenFalse()
//         {
//             //Arrange:
//             var existingItemData = new ItemData { Name = "X", Size = new Vector2Int(1, 1) };
//             var inventory = new Inventory(width: 5, height: 5, new[]
//             {
//                 new KeyValuePair<ItemData, Vector2Int>(existingItemData, new Vector2Int(3, 3))
//             });
//             var newItemData = new ItemData { Name = "Y", Size = new Vector2Int(2, 2) };
//             Item addedItem = null;
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(newItemData, new Vector2Int(2, 2));
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.IsNull(addedItem);
//         }
//
//         [Test]
//         public void WhenAddItemOnSpecifiedPositionThatIntersects_FullIntersect_ThenFalse()
//         {
//             //Arrange:
//             var existingItemData = new ItemData { Name = "X", Size = new Vector2Int(2, 2) };
//             var inventory = new Inventory(width: 3, height: 3, new[]
//             {
//                 new KeyValuePair<ItemData, Vector2Int>(existingItemData, new Vector2Int(1, 1))
//             });
//             var newItemData = new ItemData { Name = "Y", Size = new Vector2Int(2, 2) };
//             Item addedItem = null;
//             inventory.OnAdded += (i) => { addedItem = i; };
//
//             //Act:
//             bool success = inventory.AddItem(newItemData, new Vector2Int(1, 1));
//
//             //Assert:
//             Assert.IsFalse(success);
//             Assert.IsNull(addedItem);
//         }
//     }
// }