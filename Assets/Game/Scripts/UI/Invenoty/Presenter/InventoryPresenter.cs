using System.Collections.Generic;
using UnityEngine;
using Inventories;
using Sirenix.OdinInspector;

public class InventoryPresenter : MonoBehaviour
{
    [SerializeField] private InventoryView _view;
    [SerializeField] private InventoryItemCatalog _itemCatalog;
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows = 7;

    private Inventory _inventory;

    private readonly List<CellView> _highlightedCells = new();
    private Item _testItem;

    private void Start()
    {
        _inventory = new Inventory(_columns, _rows);
        _view.InitializeGrid(_columns, _rows, this);

        _inventory.AddItem(new Item(ItemID.AR_01.ToString(), 4, 2) { ItemID = ItemID.AR_01 });
        _inventory.AddItem(new Item(ItemID.AR_02.ToString(), 4, 2) { ItemID = ItemID.AR_02 });

        UpdateView();
    }

    public void RemoveItem(Item item)
    {
        Vector2Int[] cells = _inventory.GetPositions(item);
        _inventory.RemoveItem(item);
        _view.RemoveItemFromGrid(item, cells);
    }

    public bool AddItem(Item item, Vector2Int startPosition)
    {
        bool success = _inventory.AddItem(item, startPosition);
        if (!success) return false;

        Vector2Int[] positions = _inventory.GetPositions(item);
        CreateViewItem(item, positions);

        return true;
    }

    [Button]
    public void Reorganize()
    {
        _inventory.ReorganizeSpace();
        UpdateView();
    }

    [Button]
    public void AddTestItem()
    {
        _testItem = new Item(ItemID.Ring.ToString(), 1, 1) { ItemID = ItemID.Ring };
        _inventory.AddItem(_testItem);
        CreateViewItem(_testItem, _inventory.GetPositions(_testItem));
    }

    [Button]
    public void RemoveTestItem()
    {
        RemoveItem(_testItem);
    }

    public void ClearHighlights()
    {
        foreach (var cell in _highlightedCells)
            cell?.UnHighlight();

        _highlightedCells.Clear();
    }

    public void HighlightCells(Item item, Vector2Int currentPosition, Vector2Int matrixPosition, CellView currentCell)
    {
        ClearHighlights();

        Vector2Int startPos = currentPosition - matrixPosition;
        bool isCorrect = !(currentCell == null || !CanAddItem(item, startPos));
        Debug.Log(currentCell == null);
        for (int y = 0; y < item.Size.y; y++)
        {
            for (int x = 0; x < item.Size.x; x++)
            {
                Vector2Int pos = new(startPos.x + x, startPos.y + y);

                if (pos.x >= 0 && pos.y >= 0 && pos.x < _view.Cells.GetLength(0) && pos.y < _view.Cells.GetLength(1))
                {
                    var cell = _view.Cells[pos.x, pos.y];
                    _highlightedCells.Add(cell);
                }
                else
                {
                    isCorrect = false;
                }
            }
        }

        foreach (CellView cell in _highlightedCells)
            cell.Highlight(isCorrect);
    }

    private bool CanAddItem(Item item, Vector2Int position)
    {
        return _inventory.CanAddItem(item, position);
    }

    public bool MoveItem(Item draggedItem, Vector2Int targetPos)
    {
        Vector2Int[] oldPositions = _inventory.GetPositions(draggedItem);

        foreach (Vector2Int pos in oldPositions)
            _view.ClearCell(pos);

        bool success = _inventory.MoveItem(draggedItem, targetPos);

        UpdateItemPosition(draggedItem, _inventory.GetPositions(draggedItem));

        return success;
    }

    private void UpdateView()
    {
        _view.Clear();

        foreach (Item item in _inventory)
            CreateViewItem(item, _inventory.GetPositions(item));
    }

    private void CreateViewItem(Item item, Vector2Int[] positions)
    {
        Vector2Int min = GetMinPosition(positions);
        Vector2Int max = GetMaxPosition(positions);

        Vector2 size = GetViewItemSize(min, max);
        Vector2 position = _view.GetCellRectPosition(min);

        InventoryItem container = _view.CreateItemContainer(item, size, position);

        if (_itemCatalog.GetItemData(item.ItemID, out var data))
            container.SetIcon(data.Icon);

        foreach (Vector2Int pos in positions)
            _view.SetCellData(pos, item, container, pos - min, this);
    }

    private void UpdateItemPosition(Item item, Vector2Int[] positions)
    {
        Vector2Int min = GetMinPosition(positions);
        Vector2 position = _view.GetCellRectPosition(min);

        InventoryItem container = _view.GetItemContainer(item);
        container.RectTransform.anchoredPosition = position;

        foreach (Vector2Int pos in positions)
            _view.SetCellData(pos, item, container, pos - min, this);
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
            cols * _view.CellSize.x + (cols - 1) * _view.Spacing.x,
            rows * _view.CellSize.y + (rows - 1) * _view.Spacing.y
        );
    }

    public Vector2Int GetItemPosition(Item cellViewItem) => _inventory.GetPositions(cellViewItem)[0];
}