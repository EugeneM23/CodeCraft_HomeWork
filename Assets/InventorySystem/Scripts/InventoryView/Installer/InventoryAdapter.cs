using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Inventories
{
    public class InventoryAdapter
    {
        public event Action OnStateChanged;
        public event Action<Vector2Int> OnItemRemoved;
        public int Widht => _inventory.Width;
        public int Height => _inventory.Height;

        public Dictionary<string, Item> Items => _inventory.Items;

        private readonly Inventory _inventory;

        public InventoryAdapter(Inventory inventory)
        {
            _inventory = inventory;
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        [Button]
        public void RemoveItem(Vector2Int position)
        {
            Item item = _inventory.GetItem(position.x, position.y);

            if (_inventory.Contains(item.ID))
            {
                OnItemRemoved?.Invoke(GetItemPosition(item.ID));
                _inventory.RemoveItem(item.ID);
                Debug.Log($"Item {item.ID} removed from inventory");
            }
        }

        public Vector2Int GetItemPosition(string id)
        {
            Vector2Int[] positions = _inventory.GetPositions(id);
            return positions[0];
        }

        public void Reorganize()
        {
            _inventory.Reorganize();
            OnStateChanged?.Invoke();
        }
    }
}