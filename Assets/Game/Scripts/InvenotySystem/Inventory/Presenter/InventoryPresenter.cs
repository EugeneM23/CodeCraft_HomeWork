using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;
using Zenject;

public class InventoryPresenter : IInitializable, IDisposable
{
    public event Action<Item, Vector2Int[]> OnItemAdded;
    public event Action<Item, Vector2Int[]> OnItemRemoved;
    public event Action OnReorganize;

    private readonly Vector2Int _inventorySize;
    private readonly Inventory _inventory;

    public int Height => _inventorySize.y;
    public int Width => _inventorySize.x;

    public InventoryPresenter(Vector2Int inventorySize, Inventory inventory)
    {
        _inventorySize = inventorySize;
        _inventory = inventory;
    }

    public void Initialize()
    {
        _inventory.OnRemoved += HandleItemRemoved;
        _inventory.OnAdded += HandleItemAdded;
        _inventory.OnReorganize += HandleReorganize;
    }

    private void HandleReorganize()
    {
        OnReorganize?.Invoke();
    }

    public void Dispose()
    {
        _inventory.OnRemoved -= HandleItemRemoved;
        _inventory.OnAdded -= HandleItemAdded;
        _inventory.OnReorganize -= HandleReorganize;
    }

    private void HandleItemAdded(Item item, Vector2Int[] positions) => OnItemAdded?.Invoke(item, positions);

    private void HandleItemRemoved(Item item, Vector2Int[] positions) => OnItemRemoved?.Invoke(item, positions);

    public IEnumerable<KeyValuePair<Item, Vector2Int[]>> GetItems()
    {
        foreach (var item in _inventory)
        {
            Vector2Int[] positions = _inventory.GetItemGridPositions(item);
            yield return new KeyValuePair<Item, Vector2Int[]>(item, positions);
        }
    }

    public void RemoveItem(Item item) => _inventory.RemoveItem(item.ID);

    public bool AddItem(Item item, Vector2Int position) => _inventory.AddItem(item.Settings, position);

    public Vector2Int GetItemPosition(string itemID) => _inventory.GetPositions(itemID)[0];

    public bool IsFree(int x, int y) => _inventory.IsFree(x, y);

    public void Reorganize() => _inventory.Reorganize();
}