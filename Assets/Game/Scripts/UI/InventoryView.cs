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
        // Находим границы предмета
        Vector2Int min = positions[0];
        Vector2Int max = positions[0];

        foreach (var p in positions)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
        }

        // ВАЖНО: cols = ширина (по X), rows = высота (по Y)
        int cols = max.y - min.y + 1;   // количество колонок (ширина)
        int rows = max.x - min.x + 1;   // количество рядов (высота)

        // Создаём контейнер
        InventoryItem cont = Instantiate(_itemContainerPrefab, transform);
        RectTransform contRect = cont.RectTransform;
        contRect.localScale = Vector3.one;
        contRect.pivot = new Vector2(0.5f, 0.5f);

        // Размер ячейки
        RectTransform cellRect = _cells[0, 0].GetComponent<RectTransform>();
        Vector2 cellSize = cellRect.sizeDelta;

        // Устанавливаем размер контейнера
        // cols * cellSize.x = ширина (по горизонтали)
        // rows * cellSize.y = высота (по вертикали)
        contRect.sizeDelta = new Vector2(
            cols * cellSize.x,  // ширина
            rows * cellSize.y   // высота
        );

        // Вычисляем позицию центра
        RectTransform first = _cells[min.x, min.y].GetComponent<RectTransform>();
        RectTransform last  = _cells[max.x, max.y].GetComponent<RectTransform>();

        Vector3 localFirst = contRect.parent.InverseTransformPoint(first.position);
        Vector3 localLast  = contRect.parent.InverseTransformPoint(last.position);

        Vector3 center = (localFirst + localLast) * 0.5f;
        contRect.localPosition = center;

        _itemContainers[item] = cont;
        return cont;
    }
}