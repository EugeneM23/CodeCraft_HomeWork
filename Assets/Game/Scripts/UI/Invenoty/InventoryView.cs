using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private CellView _cellPrefab;
    [SerializeField] private InventoryItem _itemContainerPrefab;
    [SerializeField] private RectTransform _gridContainer;
    [SerializeField] private Vector2 _cellSize = new(100f, 100f);

    private readonly Dictionary<string, InventoryItem> _inventoryItems = new();
    private CellView[,] _cells;

    public void InitializeGrid(int columns, int rows, InventoryPresenter presenter)
    {
        _cells = new CellView[columns, rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                CreateCell(presenter, x, y);
            }
        }
    }

    private void CreateCell(InventoryPresenter presenter, int x, int y)
    {
        CellView cell = Instantiate(_cellPrefab, _gridContainer);
        cell.Construct(presenter, new Vector2Int(x, y), _cells);

        RectTransform rect = cell.GetComponent<RectTransform>();
        rect.sizeDelta = _cellSize;
        rect.anchoredPosition = new Vector2(x * _cellSize.x, -y * _cellSize.y);

        _cells[x, y] = cell;
    }

    public void CreateInventoryItem(ItemInstance instance, Vector2Int[] positions)
    {
        InventoryItem inventoryItem = Instantiate(_itemContainerPrefab, _gridContainer);

        inventoryItem.transform.position = _cells[positions[0].x, positions[0].y].transform.position;

        inventoryItem.SetItem(instance);
        _inventoryItems[instance.uniqueId] = inventoryItem;

        foreach (Vector2Int pos in positions)
        {
            _cells[pos.x, pos.y].InventoryItem = inventoryItem;
        }
    }

    public void RemoveItem(string id)
    {
        InventoryItem inventoryItem = _inventoryItems[id];
        Destroy(inventoryItem.gameObject);
    }
}