using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

public class InventoryPresenter : MonoBehaviour, IInventoryCollection
{
    [SerializeField] private InventoryView _view;
    [SerializeField] private InventoryItemCatalog _itemCatalog;
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows = 7;
    private Inventory _inventory;

    private void Awake()
    {
        _inventory = new Inventory(_columns, _rows);
        _view.InitializeGrid(_columns, _rows, this);

        UpdateView();
    }

    public Vector2Int GetItemPosition(Item cellViewItem) => _inventory.GetPositions(cellViewItem)[0];

    public bool AddItem(Item item, Vector2Int startPosition = default)
    {
        if (!TryAddToInventory(item, startPosition))
            return false;

        Vector2Int[] positions = _inventory.GetPositions(item);
        CreateViewItem(item, positions);
        return true;
    }

    public void RemoveItem(Item item)
    {
        Vector2Int[] positions = _inventory.GetPositions(item);
        _inventory.RemoveItem(item);
        _view.ClearCells(positions);
        _view.RemoveInventoryItem(item);
    }

    private bool TryAddToInventory(Item item, Vector2Int startPosition)
    {
        if (startPosition == default)
            return _inventory.AddItem(item);

        return _inventory.AddItem(item, startPosition);
    }

    [Button]
    public void Reorganize()
    {
        _inventory.ReorganizeSpace();
        UpdateView();
    }

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

        if (_itemCatalog.GetItemData(item.ItemID, out var data))
            inventoryItem.SetIcon(data.Icon);

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