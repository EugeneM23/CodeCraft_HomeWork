using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private CellView _cellPrefab;
    [SerializeField] private InventoryItem _itemContainerPrefab;
    [SerializeField] private InventoryItemCatalog _itemCatalog;
    [SerializeField] private RectTransform _gridContainer;
    [SerializeField] private Vector2 _cellSize = new(100f, 100f);
    [SerializeField] private Vector2 _spacing = new(5f, 5f);

    public CellView[,] _cells;
    private Dictionary<Item, InventoryItem> _itemContainers = new();

    public void InitializeGrid(int columns, int rows)
    {
        _cells = new CellView[columns, rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                CellView cell = Instantiate(_cellPrefab, _gridContainer);
                cell.GridPosition = new Vector2Int(x, y);

                RectTransform cellRect = cell.GetComponent<RectTransform>();
                cellRect.sizeDelta = _cellSize;
                cellRect.anchorMin = cellRect.anchorMax = cellRect.pivot = new Vector2(0, 1);
                cellRect.anchoredPosition = new Vector2(
                    x * (_cellSize.x + _spacing.x),
                    -y * (_cellSize.y + _spacing.y)
                );

                _cells[x, y] = cell;
            }
        }
    }

    public void RedrawItem(Item item, Vector2Int[] newPositions)
    {
        ClearItem(item);
        DisplayItem(item, newPositions);
    }

    private void ClearItem(Item item)
    {
        foreach (CellView cell in _cells)
            if (cell?.Item == item)
                cell.Clear();

        if (_itemContainers.Remove(item, out InventoryItem container))
            Destroy(container.gameObject);
    }

    public void Clear()
    {
        if (_cells == null) return;

        foreach (CellView cell in _cells)
            cell?.Clear();

        foreach (InventoryItem container in _itemContainers.Values)
            Destroy(container?.gameObject);

        _itemContainers.Clear();
    }

    public void DisplayItem(Item item, Vector2Int[] positions)
    {
        InventoryItem inventoryItem = SpawnInventoryItem(item, positions);

        if (_itemCatalog.GetItemData(item.ItemID, out var data))
            inventoryItem.SetIcon(data.Icon);

        Vector2Int min = positions[0];
        foreach (var p in positions)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
        }

        foreach (Vector2Int pos in positions)
        {
            CellView cell = _cells[pos.x, pos.y];
            cell.SetItem(item, inventoryItem);
            cell.ItemMatrixPosition = pos - min;
        }
    }

    private InventoryItem SpawnInventoryItem(Item item, Vector2Int[] positions)
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

        (int cols, int rows) = (max.x - min.x + 1, max.y - min.y + 1);

        InventoryItem inventoryItem = Instantiate(_itemContainerPrefab, _gridContainer);
        RectTransform contRect = inventoryItem.RectTransform;
        
        contRect.localScale = Vector3.one;
        contRect.anchorMin = contRect.anchorMax = contRect.pivot = new Vector2(0, 1);
        contRect.sizeDelta = new Vector2(
            cols * _cellSize.x + (cols - 1) * _spacing.x,
            rows * _cellSize.y + (rows - 1) * _spacing.y
        );
        contRect.anchoredPosition = _cells[min.x, min.y].GetComponent<RectTransform>().anchoredPosition;

        _itemContainers[item] = inventoryItem;
        return inventoryItem;
    }
}