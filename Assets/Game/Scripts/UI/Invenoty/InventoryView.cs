using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private CellView _cellPrefab;
    [SerializeField] private InventoryItem _itemContainerPrefab;
    [SerializeField] private RectTransform _gridContainer;
    [SerializeField] private Vector2 _cellSize = new(100f, 100f);

    private readonly Dictionary<Item, InventoryItem> _inventoryItems = new();
    public CellView[,] Cells { get; private set; }
    public Vector2 CellSize => _cellSize;

    public void InitializeGrid(int columns, int rows, InventoryPresenter presenter)
    {
        Cells = new CellView[columns, rows];

        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
            CreateCell(presenter, x, y);
    }

    private void CreateCell(InventoryPresenter presenter, int x, int y)
    {
        CellView cell = Instantiate(_cellPrefab, _gridContainer);
        cell.Construct(presenter, new Vector2Int(x, y));

        RectTransform rect = cell.GetComponent<RectTransform>();
        rect.sizeDelta = _cellSize;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x * _cellSize.x, -y * _cellSize.y);

        Cells[x, y] = cell;
    }

    public InventoryItem CreateInventoryItem(Item item, Vector2 size, Vector2 position)
    {
        InventoryItem container = Instantiate(_itemContainerPrefab, _gridContainer);
        container.SetItem(item);

        RectTransform rect = container.RectTransform;
        rect.localScale = Vector3.one;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        _inventoryItems[item] = container;
        return container;
    }

    public void AddInventoryItem(InventoryItem item)
    {
        _inventoryItems[item.Item] = item;
    }

    public void AssignItemToCell(InventoryItem inventoryItem, Vector2Int[] positions, Vector2Int minPosition)
    {
        foreach (Vector2Int pos in positions)
        {
            CellView cell = Cells[pos.x, pos.y];
            cell.Construct(inventoryItem, pos - minPosition);
        }
    }

    public void PlaceInventoryItem(InventoryItem inventoryItem, Vector2Int[] positions)
    {
        Vector2Int minPos = positions[0];
        foreach (Vector2Int p in positions)
        {
            if (p.x < minPos.x) minPos.x = p.x;
            if (p.y < minPos.y) minPos.y = p.y;
        }

        Vector2 position = GetCellPosition(minPos);
        inventoryItem.RectTransform.SetParent(_gridContainer, false);
        inventoryItem.RectTransform.anchoredPosition = position;
        inventoryItem.RectTransform.localScale = Vector3.one;

        AssignItemToCell(inventoryItem, positions, minPos);
    }

    public Vector2 GetCellPosition(Vector2Int gridPos)
    {
        return Cells[gridPos.x, gridPos.y].GetComponent<RectTransform>().anchoredPosition;
    }

    public void ClearGrid()
    {
        if (Cells == null) return;

        foreach (CellView cell in Cells)
            cell?.Clear();

        foreach (InventoryItem container in _inventoryItems.Values)
            if (container != null)
                Destroy(container.gameObject);

        _inventoryItems.Clear();
    }

    public void ClearCells(Vector2Int[] positions)
    {
        foreach (Vector2Int pos in positions)
            Cells[pos.x, pos.y].Clear();
    }

    public void RemoveInventoryItem(InventoryItem draggedInventoryItem)
    {
        _inventoryItems[draggedInventoryItem.Item] = null;
    }
}