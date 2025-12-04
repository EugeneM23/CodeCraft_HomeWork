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
        FillCellsData(item, inventoryItem, positions);

        return true;
    }

    private void FillCellsData(Item item, InventoryItem inventoryItem, Vector2Int[] positions)
    {
        Vector2Int min = GetMinPosition(positions);

        foreach (Vector2Int pos in positions)
            _view.SetCellData(pos, item, inventoryItem, pos - min);
    }

    public void RemoveItem(Item item)
    {
        Vector2Int[] cells = _inventory.GetPositions(item);
        _inventory.RemoveItem(item);
        _view.RemoveItemFromGrid(item, cells);
    }

    public void RemoveItemFromInventory(Item item)
    {
        Vector2Int[] positions = _inventory.GetPositions(item);
        _view.ClearCell(positions);
        _inventory.RemoveItem(item);
    }

    public bool MoveItem(Item item, Vector2Int targetPos)
    {
        Vector2Int[] oldPositions = _inventory.GetPositions(item);

        foreach (Vector2Int pos in oldPositions)
            _view.ClearCell(pos);

        bool success = _inventory.MoveItem(item, targetPos);

        UpdateViewItemPosition(item, _inventory.GetPositions(item));

        return success;
    }
}