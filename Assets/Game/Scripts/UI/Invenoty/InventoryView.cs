using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private CellView _cellPrefab;
    [SerializeField] private InventoryItem _itemContainerPrefab;
    [SerializeField] private RectTransform _gridContainer;
    [SerializeField] private Vector2 _cellSize = new Vector2(100f, 100f);

    private readonly Dictionary<string, InventoryItem> _inventoryItems = new Dictionary<string, InventoryItem>();
    private CellView[,] _cells;

    public void InitializeGrid(int columns, int rows, Inventory inventory)
    {
        _cells = new CellView[columns, rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                CreateCell(inventory, x, y);
            }
        }
    }

    public void AddItem(ItemInstance instance, Vector2Int[] positions)
    {
        InventoryItem inventoryItem = Instantiate(_itemContainerPrefab, _gridContainer);
        inventoryItem.transform.position = _cells[positions[0].x, positions[0].y].transform.position;
        
        Debug.Log(instance.ItemConsumer == null);
        inventoryItem.SetupItem(instance, _cellSize);

        _inventoryItems[instance.ID] = inventoryItem;

        foreach (Vector2Int pos in positions)
        {
            _cells[pos.x, pos.y].InventoryItem = inventoryItem;
        }
    }

    public void RemoveItem(string id)
    {
        InventoryItem item = _inventoryItems[id];

        foreach (CellView cell in _cells)
        {
            if (cell.InventoryItem == item)
                cell.Clear();
        }

        _inventoryItems.Remove(id);
    }

    public CellView GetCell(Vector2Int cellIndex)
    {
        return _cells[cellIndex.x, cellIndex.y];
    }

    private void CreateCell(Inventory inventory, int x, int y)
    {
        CellView cell = Instantiate(_cellPrefab, _gridContainer);
        cell.Construct(inventory, new Vector2Int(x, y));

        RectTransform rect = cell.GetComponent<RectTransform>();
        rect.sizeDelta = _cellSize;
        rect.anchoredPosition = new Vector2(x * _cellSize.x, -y * _cellSize.y);

        _cells[x, y] = cell;
    }
}