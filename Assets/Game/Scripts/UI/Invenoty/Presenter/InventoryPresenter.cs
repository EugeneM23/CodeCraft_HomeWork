using System;
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

    private void Awake()
    {
        _inventory = new Inventory(_columns, _rows);
        _view.InitializeGrid(_columns, _rows, this);

        UpdateView();
    }

    public bool AddItem(Item item, Vector2Int startPosition = default)
    {
        bool success;
        if (startPosition == default) 
            success =_inventory.AddItem(item);
        else
            success =_inventory.AddItem(item, startPosition);

        if (!success) return false;

        Vector2Int[] positions = _inventory.GetPositions(item);
        CreateViewItem(item, positions);

        return true;
    }

    private bool CanAddItem(Item item, Vector2Int position) => _inventory.CanAddItem(item, position);

    public void RemoveItem(Item item)
    {
        Vector2Int[] cells = _inventory.GetPositions(item);
        _inventory.RemoveItem(item);
        _view.RemoveItemFromGrid(item, cells);
    }

    [Button]
    public void Reorganize()
    {
        _inventory.ReorganizeSpace();
        UpdateView();
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

    public bool MoveItem(Item item, Vector2Int targetPos)
    {
        Vector2Int[] oldPositions = _inventory.GetPositions(item);

        foreach (Vector2Int pos in oldPositions)
            _view.ClearCell(pos);

        bool success = _inventory.MoveItem(item, targetPos);

        UpdateViewItemPosition(item, _inventory.GetPositions(item));

        return success;
    }

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

    public Vector2Int GetItemPosition(Item cellViewItem) => _inventory.GetPositions(cellViewItem)[0];
}