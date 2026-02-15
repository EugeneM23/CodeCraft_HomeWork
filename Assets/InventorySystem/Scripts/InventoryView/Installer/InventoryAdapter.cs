using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryAdapter : IInitializable
    {
        public event Action<Item, Vector2Int[]> OnItemAdded;
        public event Action<Item, Vector2Int[]> OnItemRemoved;
        public event Action OnStateChanged;

        public int Width => _inventory.Width;
        public int Height => _inventory.Height;
        public Dictionary<string, Item> Items => _inventory.Items;
        public Vector2Int CellSize { get; }
        public RectTransform InventoryItemPrefab { get; }
        public Cell CellPrefab { get; }
        public RectTransform GridContainer { get; }

        private readonly Inventory _inventory;

        public InventoryAdapter(
            Inventory inventory,
            Vector2Int cellSize,
            RectTransform inventoryItemPrefab,
            Cell cellPrefab,
            RectTransform gridContainer)
        {
            _inventory = inventory;
            CellSize = cellSize;
            InventoryItemPrefab = inventoryItemPrefab;
            CellPrefab = cellPrefab;
            GridContainer = gridContainer;
        }

        public void Initialize()
        {
            _inventory.OnAdded += (item, positions) => OnItemAdded?.Invoke(item, positions);
            _inventory.OnRemoved += RemoveItem;
        }

        private void RemoveItem(Item item, Vector2Int[] positions)
        {
            OnItemRemoved?.Invoke(item, positions);
        }

        public Vector2Int[] GetItemPosition(string id)
        {
            var positions = _inventory.GetPositions(id);
            return positions;
        }

        public void AddItem(ItemData itemData)
        {
            _inventory.AddItem(itemData);
        }


        public void RemoveItem(string itemID)
        {
            if (_inventory.RemoveItem(itemID))
            {
                Debug.Log("removed");
            }
            else
            {
                Debug.Log("not found");
            }
        }

        public void Reorganize()
        {
            _inventory.Reorganize();
            //OnStateChanged?.Invoke();
        }
    }
}