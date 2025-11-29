using System;
using Inventories;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Game.Scripts.UI
{
    public class Bag : MonoBehaviour
    {
        public event Action OnStateChanged;
        public Inventory inventory;

        private void Awake()
        {
            inventory = new Inventory(10, 10);
            Item x = new Item(ItemID.Item_01.ToString(), 2, 2);
            Item y = new Item(ItemID.Item_02.ToString(), 1, 2);
            Item z = new Item(ItemID.Item_02.ToString(), 1, 1);

            inventory.AddItem(x, 4, 0);
            inventory.AddItem(y, 5, 5);
            inventory.AddItem(z, 8, 8);
        }

        [Button]
        public void ReorganizeSpace()
        {
            inventory.ReorganizeSpace();
            OnStateChanged?.Invoke();
        }
    }
}