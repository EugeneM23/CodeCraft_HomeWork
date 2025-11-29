using System;
using Inventories;
using UnityEditor;
using UnityEngine;

namespace Game.Scripts.UI
{
    public class Bag : MonoBehaviour
    {
        public Inventory inventory;

        private void Awake()
        {
            inventory = new Inventory(10, 10);
            Item x = new Item(ItemID.Item_01.ToString(), 1, 1);
            Item y = new Item(ItemID.Item_02.ToString(), 2, 2);
            Item z = new Item(ItemID.Item_02.ToString(), 2, 2);

            inventory.AddItem(x, 4, 0);
            inventory.AddItem(y, 5, 5);
            inventory.AddItem(z, 8, 8);
        }
    }
}