using Inventories;
using UnityEngine;

public partial class InventoryPresenter
{
    public bool AddItem(Item item, Vector2Int startPosition = default)
    {
        if (!TryAddToInventory(item, startPosition))
            return false;

        Vector2Int[] positions = _inventory.GetPositions(item);
        CreateViewItem(item, positions);
        return true;
    }

    public bool MoveItem(Item item, InventoryItem inventoryItem, Vector2Int startPosition = default)
    {
        if (!TryAddToInventory(item, startPosition))
            return false;

        Vector2Int[] positions = _inventory.GetPositions(item);
        _view.PlaceInventoryItem(inventoryItem, positions);
        return true;
    }

    public void RemoveItem(Item item)
    {
        Vector2Int[] positions = _inventory.GetPositions(item);
        _inventory.RemoveItem(item);
        _view.ClearCells(positions);
    }

    private bool TryAddToInventory(Item item, Vector2Int startPosition)
    {
        if (startPosition == default)
            return _inventory.AddItem(item);

        return _inventory.AddItem(item, startPosition);
    }

    public void AddViewItemToView(InventoryItem item) => _view.AddInventoryItem(item);

    public void RemoveFromViewItem(InventoryItem draggedInventoryItem) => _view.RemoveInventoryItem(draggedInventoryItem);
}