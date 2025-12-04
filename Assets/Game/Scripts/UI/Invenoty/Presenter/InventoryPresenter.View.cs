using Inventories;
using UnityEngine;

public partial class InventoryPresenter
{
    private void UpdateView()
    {
        _view.ClearGrid();

        foreach (Item item in _inventory)
        {
            Vector2Int[] positions = _inventory.GetPositions(item);
            CreateViewItem(item, positions);
        }
    }

    private void CreateViewItem(Item item, Vector2Int[] positions)
    {
        ItemBounds bounds = CalculateBounds(positions);
        InventoryItem inventoryItem = _view.CreateInventoryItem(item, bounds.Size, bounds.Position);

        Debug.Log(item.ItemID);
        if (_itemCatalog.GetItemData(item.ItemID, out var data))
        {
            inventoryItem.SetIcon(data.Icon);
        }

        _view.AssignItemToCell(inventoryItem, positions, bounds.Min);
    }

    private ItemBounds CalculateBounds(Vector2Int[] positions)
    {
        Vector2Int min = positions[0];
        Vector2Int max = positions[0];

        for (int i = 1; i < positions.Length; i++)
        {
            Vector2Int p = positions[i];
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
        }

        int cols = max.x - min.x + 1;
        int rows = max.y - min.y + 1;
        Vector2 size = new Vector2(
            cols * _view.CellSize.x + (cols - 1),
            rows * _view.CellSize.y + (rows - 1)
        );

        return new ItemBounds(min, max, size, _view.GetCellPosition(min));
    }

    private readonly struct ItemBounds
    {
        public readonly Vector2Int Min;
        public readonly Vector2Int Max;
        public readonly Vector2 Size;
        public readonly Vector2 Position;

        public ItemBounds(Vector2Int min, Vector2Int max, Vector2 size, Vector2 position)
        {
            Min = min;
            Max = max;
            Size = size;
            Position = position;
        }
    }

}