using System.Collections.Generic;
using Inventories;
using UnityEditor;
using UnityEngine;

namespace Game.Scripts.UI.Equipment
{
    public class Equipment : MonoBehaviour
    {
        private Dictionary<Item, Vector2Int> _items = new();

        public bool AddItem(Item item, Vector2Int startPosition = default)
        {
            _items.Add(item, startPosition);

            return true;
        }

        public void RemoveItem(Item item)
        {
            _items.Remove(item);
        }
    }
}