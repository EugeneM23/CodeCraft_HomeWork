using Inventories;
using UnityEngine;

public partial class InventoryPresenter
{
    public bool AddItem(Item item, Vector2Int startPosition = default)
    {
        bool success;
        if (startPosition == default)
            success = _inventory.AddItem(item);
        else
            success = _inventory.AddItem(item, startPosition);

        if (!success) return false;

        Vector2Int[] positions = _inventory.GetPositions(item);
        CreateViewItem(item, positions);

        return true;
    }

    public bool AddItemToInventory(Item item, InventoryItem inventoryItem, Vector2Int startPosition = default)
    {
        bool success = startPosition == default
            ? _inventory.AddItem(item)
            : _inventory.AddItem(item, startPosition);

        if (!success) return false;

        Vector2Int[] positions = _inventory.GetPositions(item);
        Vector2Int min = GetMinPosition(positions);
        Vector2 position = _view.GetCellRectPosition(min);

        _view.ReparentInventoryItem(inventoryItem, position);
        FillCellsData(inventoryItem, positions);

        return true;
    }

    private void FillCellsData(InventoryItem inventoryItem, Vector2Int[] positions)
    {
        Vector2Int min = GetMinPosition(positions);

        foreach (Vector2Int pos in positions)
            _view.SetCellData(pos, inventoryItem, pos - min);
    }

    public void RemoveItemFromInventory(Item item)
    {
        Vector2Int[] positions = _inventory.GetPositions(item);
        _view.ClearCell(positions);
        _inventory.RemoveItem(item);
    }
}