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

    private readonly Dictionary<Item, InventoryItem> _itemContainers = new();
    public CellView[,] Cells { get; private set; }

    public Vector2 CellSize => _cellSize;
    public Vector2 Spacing => _spacing;

    public void InitializeGrid(int columns, int rows)
    {
        Cells = new CellView[columns, rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                CellView cell = Instantiate(_cellPrefab, _gridContainer);
                cell.GridPosition = new Vector2Int(x, y);

                RectTransform rect = cell.GetComponent<RectTransform>();
                rect.sizeDelta = _cellSize;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(
                    x * (_cellSize.x + _spacing.x),
                    -y * (_cellSize.y + _spacing.y)
                );

                Cells[x, y] = cell;
            }
        }
    }

    public void SetCellData(Vector2Int pos, Item item, InventoryItem container, Vector2Int matrixPos)
    {
        CellView cell = Cells[pos.x, pos.y];
        cell.SetItem(item, container);
        cell.ItemMatrixPosition = matrixPos;
    }

    public void ClearCell(Vector2Int pos)
    {
        Cells[pos.x, pos.y].Clear();
    }

    public CellView GetCell(Vector2Int pos)
    {
        return Cells[pos.x, pos.y];
    }

    public InventoryItem GetItemContainer(Item item)
    {
        return _itemContainers[item];
    }

    public InventoryItem CreateItemContainer(Item item, Vector2 size, Vector2 position)
    {
        InventoryItem container = Instantiate(_itemContainerPrefab, _gridContainer);
        RectTransform rect = container.RectTransform;

        rect.localScale = Vector3.one;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        _itemContainers[item] = container;
        return container;
    }

    public Vector2 GetCellPosition(Vector2Int gridPos)
    {
        return Cells[gridPos.x, gridPos.y].GetComponent<RectTransform>().anchoredPosition;
    }

    public void Clear()
    {
        if (Cells == null) return;

        foreach (CellView cell in Cells)
            cell?.Clear();

        foreach (InventoryItem container in _itemContainers.Values)
            if (container != null)
                Destroy(container.gameObject);

        _itemContainers.Clear();
    }

    public void RemoveItem(Item item, Vector2Int[] cellsPositions)
    {
        if (_itemContainers.ContainsKey(item))
        {
            InventoryItem container = _itemContainers[item];

            foreach (Vector2Int position in cellsPositions)
                ClearCell(position);

            _itemContainers.Remove(item);
            Destroy(container.gameObject);
        }
    }

    public void AddItem(Item item, Vector2Int startPosition)
    {
        
    }
}