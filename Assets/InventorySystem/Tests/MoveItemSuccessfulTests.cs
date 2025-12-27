// using System.Collections.Generic;
// using NUnit.Framework;
// using UnityEngine;
//
// namespace Inventories
// {
//     [TestFixture]
//     public class MoveItemSuccessfulTests
//     {
//         [Test]
//         public void MoveItemSuccessful_Simple()
//         {
//             //Arrange:
//             var item1 = new Item(1, 1);
//             var inventory = new Inventory(5, 5, new KeyValuePair<Item, Vector2Int>(item1, new Vector2Int(0, 0)));
//             var position = new Vector2Int(1, 1);
//
//             Item movedItem = default;
//             Vector2Int movedPosition = Vector2Int.zero;
//             inventory.OnMoved += (i, p) =>
//             {
//                 movedItem = i;
//                 movedPosition = p;
//             };
//
//             bool added = false;
//             bool removed = false;
//             inventory.OnAdded += (_, _) => added = true;
//             inventory.OnRemoved += (_, _) => removed = true;
//
//             //Act:
//             bool success = inventory.MoveItem(item1, position);
//             Debug.Log(success);
//
//             //Assert:
//             Assert.IsTrue(success);
//             Assert.AreEqual(item1, movedItem);
//             Assert.AreEqual(position, movedPosition);
//
//             foreach (Vector2Int newPosition in inventory.GetPositions(item1))
//             {
//                 Assert.AreEqual(item1, inventory.GetItem(newPosition));
//             }
//
//             Assert.IsFalse(added);
//             Assert.IsFalse(removed);
//         }
//
//         [Test]
//         public void MoveItemSuccessful_IntersectsWithItself()
//         {
//             //Arrange:
//             var item2 = new Item(2, 2);
//             var inventory = new Inventory(5, 5, new KeyValuePair<Item, Vector2Int>(item2, new Vector2Int(0, 0)));
//             var position = new Vector2Int(1, 1);
//
//             Item movedItem = default;
//             Vector2Int movedPosition = Vector2Int.zero;
//             inventory.OnMoved += (i, p) =>
//             {
//                 movedItem = i;
//                 movedPosition = p;
//             };
//
//             bool added = false;
//             bool removed = false;
//             inventory.OnAdded += (_, _) => added = true;
//             inventory.OnRemoved += (_, _) => removed = true;
//
//             //Act:
//             bool success = inventory.MoveItem(item2, position);
//             Debug.Log(success);
//
//             //Assert:
//             Assert.IsTrue(success);
//             Assert.AreEqual(item2, movedItem);
//             Assert.AreEqual(position, movedPosition);
//
//             foreach (Vector2Int newPosition in inventory.GetPositions(item2))
//             {
//                 Assert.AreEqual(item2, inventory.GetItem(newPosition));
//             }
//
//             Assert.IsFalse(added);
//             Assert.IsFalse(removed);
//         }
//     }
// }