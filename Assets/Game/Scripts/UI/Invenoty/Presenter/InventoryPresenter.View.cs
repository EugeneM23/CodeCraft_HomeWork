using Inventories;
using UnityEngine;

public partial class InventoryPresenter
{
    private void UpdateView()
    {
        _view.ClearGrid();

        foreach (Item item in _inventory)
            CreateViewItem(item, _inventory.GetPositions(item));
    }

    private void CreateViewItem(Item item, Vector2Int[] positions)
    {
        Vector2Int min = GetMinPosition(positions);
        Vector2Int max = GetMaxPosition(positions);
        Vector2 size = GetViewItemSize(min, max);
        Vector2 position = _view.GetCellRectPosition(min);

        InventoryItem inventoryItem = _view.CreateInventoryItem(item, size, position);

        if (_itemCatalog.GetItemData(item.ItemID, out var data))
            inventoryItem.SetIcon(data.Icon);

        foreach (Vector2Int pos in positions)
            _view.SetCellData(pos, item, inventoryItem, pos - min);
    }

    private void UpdateViewItemPosition(Item item, Vector2Int[] positions)
    {
        Vector2Int min = GetMinPosition(positions);
        Vector2 position = _view.GetCellRectPosition(min);

        InventoryItem inventoryItem = _view.GetInventoryItem(item);
        inventoryItem.RectTransform.anchoredPosition = position;

        foreach (Vector2Int pos in positions)
            _view.SetCellData(pos, item, inventoryItem, pos - min);
    }

    private Vector2Int GetMinPosition(Vector2Int[] positions)
    {
        Vector2Int min = positions[0];

        foreach (Vector2Int p in positions)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
        }

        return min;
    }

    private Vector2Int GetMaxPosition(Vector2Int[] positions)
    {
        Vector2Int max = positions[0];

        foreach (Vector2Int p in positions)
        {
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
        }

        return max;
    }

    private Vector2 GetViewItemSize(Vector2Int min, Vector2Int max)
    {
        int cols = max.x - min.x + 1;
        int rows = max.y - min.y + 1;

        return new Vector2(
            cols * _view.CellSize.x + (cols - 1),
            rows * _view.CellSize.y + (rows - 1)
        );
    }
}