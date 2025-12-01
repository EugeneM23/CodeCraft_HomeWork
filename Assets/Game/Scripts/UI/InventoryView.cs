using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private CellView _cellPrefab;
    [SerializeField] private InventoryItem _itemContainerPrefab;
    [SerializeField] private RectTransform _gridContainer;
    [SerializeField] private Vector2 _cellSize = new(100f, 100f);
    [SerializeField] private Vector2 _spacing = new(5f, 5f);

    public CellView[,] _cells;
    private Dictionary<Item, InventoryItem> _itemContainers = new();

    public Vector2 CellSize => _cellSize;
    public Vector2 Spacing => _spacing;

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

    public void SetItemIcon(InventoryItem inventoryItem, Sprite icon)
    {
        inventoryItem.SetIcon(icon);
    }

    public void SetCellData(Vector2Int pos, Item item, InventoryItem inventoryItem, Vector2Int matrixPosition)
    {
        CellView cell = _cells[pos.x, pos.y];
        cell.SetItem(item, inventoryItem);
        cell.ItemMatrixPosition = matrixPosition;
    }

    public InventoryItem CreateItemContainer(Vector2 size, Vector2 position)
    {
        InventoryItem inventoryItem = Instantiate(_itemContainerPrefab, _gridContainer);
        RectTransform contRect = inventoryItem.RectTransform;

        contRect.localScale = Vector3.one;
        contRect.anchorMin = contRect.anchorMax = contRect.pivot = new Vector2(0, 1);
        contRect.sizeDelta = size;
        contRect.anchoredPosition = position;

        return inventoryItem;
    }

    public void RemoveItemContainer(Item item)
    {
        if (_itemContainers.Remove(item, out InventoryItem container))
            Destroy(container.gameObject);
    }

    public void RegisterItemContainer(Item item, InventoryItem container)
    {
        _itemContainers[item] = container;
    }

    public void ClearCell(Vector2Int pos)
    {
        _cells[pos.x, pos.y].Clear();
    }

    public Vector2 GetCellPosition(Vector2Int gridPos)
    {
        return _cells[gridPos.x, gridPos.y].GetComponent<RectTransform>().anchoredPosition;
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
}