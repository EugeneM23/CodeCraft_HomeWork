using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;
using Zenject;

public class InventoryAdapterN : IInitializable, IDisposable
{
    public event Action<Item, Vector2Int[]> OnItemAdded;
    public event Action<Item, Vector2Int[]> OnItemRemoved;

    private readonly Vector2Int _inventorySize;
    private readonly Inventory _inventory;

    public int Height => _inventorySize.y;
    public int Width => _inventorySize.x;

    public InventoryAdapterN(Vector2Int inventorySize, Inventory inventory)
    {
        _inventorySize = inventorySize;
        _inventory = inventory;
    }

    public void Initialize()
    {
        _inventory.OnRemoved += HandleItemRemoved;
        _inventory.OnAdded += HandleItemAdded;
    }

    public void Dispose()
    {
        _inventory.OnRemoved -= HandleItemRemoved;
        _inventory.OnAdded -= HandleItemAdded;
    }

    private void HandleItemAdded(Item item, Vector2Int[] positions)
    {
        OnItemAdded?.Invoke(item, positions);
    }

    private void HandleItemRemoved(Item item, Vector2Int[] positions)
    {
        OnItemRemoved?.Invoke(item, positions);
    }

    public IEnumerable<KeyValuePair<Item, Vector2Int[]>> GetItems()
    {
        foreach (var item in _inventory)
        {
            Vector2Int[] positions = _inventory.GetItemGridPositions(item);
            yield return new KeyValuePair<Item, Vector2Int[]>(item, positions);
        }
    }

    public void RemoveItem(Item item)
    {
        _inventory.RemoveItem(item.ID);
    }

    public bool AddItem(Item item, Vector2Int position)
    {
        return _inventory.AddItem(item.itemData, position);
    }

    public Vector2Int GetItemPosition(string itemID)
    {
        return _inventory.GetPositions(itemID)[0];
    }
}