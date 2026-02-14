using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryAdapter : IInitializable, IDisposable
    {
        public event Action OnStateChanged;
        public event Action<Vector2Int> OnItemRemoved;
        
        public int Width => _inventory.Width;
        public int Height => _inventory.Height;
        public Dictionary<string, Item> Items => _inventory.Items;
        public Vector2Int CellSize { get; }
        public RectTransform InventoryItemPrefab { get; }
        public CellView CellPrefab { get; }
        public RectTransform GridContainer { get; }

        private readonly Inventory _inventory;
        private readonly SignalBus _signalBus;
        private readonly Dictionary<Vector2Int, InventoryItem> _itemsCache = new();

        public InventoryAdapter(
            Inventory inventory, 
            Vector2Int cellSize,
            RectTransform inventoryItemPrefab,
            CellView cellPrefab,
            RectTransform gridContainer,
            SignalBus signalBus)
        {
            _inventory = inventory;
            CellSize = cellSize;
            InventoryItemPrefab = inventoryItemPrefab;
            CellPrefab = cellPrefab;
            GridContainer = gridContainer;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<ItemRemovedSignal>(OnItemRemovedSignal);
        }

        public void Dispose()
        {
            _signalBus.Unsubscribe<ItemRemovedSignal>(OnItemRemovedSignal);
        }

        private void OnItemRemovedSignal(ItemRemovedSignal signal)
        {
            var position = _itemsCache.FirstOrDefault(x => x.Value == signal.Item).Key;
            RemoveItem(position);
        }

        public void RegisterItem(Vector2Int position, InventoryItem item)
        {
            _itemsCache[position] = item;
        }

        public void RemoveItem(Vector2Int position)
        {
            var item = _inventory.GetItem(position.x, position.y);
            var itemPosition = GetItemPosition(item.ID);
            
            _inventory.RemoveItem(item.ID);
            _itemsCache.Remove(itemPosition);
            OnItemRemoved?.Invoke(itemPosition);
        }

        public Vector2Int GetItemPosition(string id)
        {
            var positions = _inventory.GetPositions(id);
            return positions[0];
        }

        public void Reorganize()
        {
            _inventory.Reorganize();
            OnStateChanged?.Invoke();
        }
    }
}