using Inventories;
using UnityEngine;

public partial class InventoryPresenter
{
    private void UpdateItemPosition(Item item, Vector2Int[] positions)
    {
        Vector2Int min = positions[0];
        Vector2Int max = positions[0];

        foreach (Vector2Int p in positions)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
        }

        Vector2 position = _view.GetCellPosition(min);
        InventoryItem container = _view.GetItemContainer(item);
        container.RectTransform.anchoredPosition = position;

        foreach (Vector2Int pos in positions)
            _view.SetCellData(pos, item, container, pos - min);
    }

    public bool MoveItem(Item draggedItem, Vector2Int targetPos)
    {
        Vector2Int[] vector2Ints = _inventory.GetPositions(draggedItem);

        foreach (Vector2Int pos in vector2Ints)
            _view.ClearCell(pos);

        bool moveItem = _inventory.MoveItem(draggedItem, targetPos);

        UpdateItemPosition(draggedItem, _inventory.GetPositions(draggedItem));

        return moveItem;
    }

    private void UpdateView()
    {
        _view.Clear();
        foreach (Item item in _inventory)
            CreateViewItem(item, _inventory.GetPositions(item));
    }

    private void CreateViewItem(Item item, Vector2Int[] positions)
    {
        Vector2Int min = positions[0];
        Vector2Int max = positions[0];

        foreach (Vector2Int p in positions)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
        }

        int cols = max.x - min.x + 1;
        int rows = max.y - min.y + 1;
        Vector2 size = new Vector2(
            cols * _view.CellSize.x + (cols - 1) * _view.Spacing.x,
            rows * _view.CellSize.y + (rows - 1) * _view.Spacing.y
        );

        Vector2 position = _view.GetCellPosition(min);
        InventoryItem container = _view.CreateItemContainer(item, size, position);

        if (_itemCatalog.GetItemData(item.ItemID, out var data))
            container.SetIcon(data.Icon);

        foreach (Vector2Int pos in positions)
            _view.SetCellData(pos, item, container, pos - min);
    }
}