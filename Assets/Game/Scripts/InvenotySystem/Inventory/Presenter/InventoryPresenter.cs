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
    private readonly InventoryModel _inventoryModel;

    public int Height => _inventoryModel.Height;
    public int Width => _inventoryModel.Width;

    public InventoryPresenter(InventoryModel inventoryModel)
    {
        _inventoryModel = inventoryModel;
    }

    public void Initialize()
    {
        _inventoryModel.OnItemRemoved += HandleItemItemRemoved;
        _inventoryModel.OnItemAdded += HandleItemItemAdded;
        _inventoryModel.OnReorganize += HandleReorganize;
    }

    private void HandleReorganize()
    {
        OnReorganize?.Invoke();
    }

    public void Dispose()
    {
        _inventoryModel.OnItemRemoved -= HandleItemItemRemoved;
        _inventoryModel.OnItemAdded -= HandleItemItemAdded;
        _inventoryModel.OnReorganize -= HandleReorganize;
    }

    private void HandleItemItemAdded(Item item, Vector2Int[] positions) => OnItemAdded?.Invoke(item, positions);

    private void HandleItemItemRemoved(Item item, Vector2Int[] positions) => OnItemRemoved?.Invoke(item, positions);

    public IEnumerable<KeyValuePair<Item, Vector2Int[]>> GetItems()
    {
        foreach (var item in _inventoryModel)
        {
            Vector2Int[] positions = _inventoryModel.GetItemGridPositions(item);
            yield return new KeyValuePair<Item, Vector2Int[]>(item, positions);
        }
    }

    public void RemoveItem(Item item) => _inventoryModel.RemoveItem(item.ID);

    public bool AddItem(Item item, Vector2Int position) => _inventoryModel.AddItem(item.Settings, position);

    public Vector2Int GetItemPosition(string itemID) => _inventoryModel.GetPositions(itemID)[0];

    public bool IsFree(int x, int y) => _inventoryModel.IsFree(x, y);

    public void Reorganize() => _inventoryModel.Reorganize();
}