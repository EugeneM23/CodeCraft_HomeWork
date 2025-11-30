using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    public event Action<Item, Vector2Int, Vector2Int> OnItemDragged;

    [SerializeField] private GridToMatrix _gridToMatrix;
    [SerializeField] private InventoryItem _itemContainerPrefab;
    
    private CellView[,] _cells;
    private Dictionary<Item, InventoryItem> _itemContainers = new();

    private void Awake()
    {
        _cells = _gridToMatrix.BuildMatrix();
        InitializeCells();
    }

    private void InitializeCells()
    {
        for (int x = 0; x < _cells.GetLength(0); x++)
        for (int y = 0; y < _cells.GetLength(1); y++)
            _cells[x, y].GridPosition = new Vector2Int(x, y);
    }

    public void RequestMoveItem(Item item, Vector2Int fromPosition, Vector2Int toPosition)
    {
        OnItemDragged?.Invoke(item, fromPosition, toPosition);
    }

    public void Clear()
    {
        foreach (CellView cell in _cells)
            cell.Clear();
        
        foreach (InventoryItem container in _itemContainers.Values)
            Destroy(container.gameObject);
        
        _itemContainers.Clear();
    }

    public void DisplayItem(Item item, Vector2Int[] positions)
    {
        InventoryItem container = SpawnItemContainer(item, positions);

        foreach (Vector2Int pos in positions)
            _cells[pos.x, pos.y].SetItem(item, container);
    }

    private InventoryItem SpawnItemContainer(Item item, Vector2Int[] positions)
    {
        // Находим границы
        Vector2Int min = positions[0];
        Vector2Int max = positions[0];
        
        foreach (Vector2Int pos in positions)
        {
            if (pos.x < min.x) min.x = pos.x;
            if (pos.y < min.y) min.y = pos.y;
            if (pos.x > max.x) max.x = pos.x;
            if (pos.y > max.y) max.y = pos.y;
        }

        // Спавним контейнер
        InventoryItem container = Instantiate(_itemContainerPrefab, transform);
        RectTransform rect = container.RectTransform;
        
        // Размер одной ячейки
        Vector2 cellSize = _cells[0, 0].GetComponent<RectTransform>().sizeDelta;
        
        // Размер контейнера
        rect.sizeDelta = new Vector2(
            cellSize.x * (max.x - min.x + 1),
            cellSize.y * (max.y - min.y + 1)
        );
        
        // Позиция (центр между первой и последней ячейкой)
        RectTransform firstRect = _cells[min.x, min.y].GetComponent<RectTransform>();
        RectTransform lastRect = _cells[max.x, max.y].GetComponent<RectTransform>();
        
        rect.position = (firstRect.position + lastRect.position) / 2f;
        
        _itemContainers[item] = container;
        return container;
    }
}