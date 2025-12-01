using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    public event Action<Item, Vector2Int, Vector2Int> OnItemDragged;

    [SerializeField] private CellView _cellPrefab;
    [SerializeField] private InventoryItem _itemContainerPrefab;
    [SerializeField] private InventoryItemCatalog _itemCatalog;
    [SerializeField] private RectTransform _gridContainer; // Контейнер для всей сетки
    [SerializeField] private Vector2 _cellSize = new(100f, 100f); // Размер одной ячейки
    [SerializeField] private Vector2 _spacing = new(5f, 5f); // Отступы между ячейками

    public CellView[,] _cells;
    private Dictionary<Item, InventoryItem> _itemContainers = new();

    public void InitializeGrid(int columns, int rows)
    {
        ClearGrid();

        _cells = new CellView[columns, rows];

        // Создаём ячейки и расставляем их вручную
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                CellView cell = Instantiate(_cellPrefab, _gridContainer);
                cell.GridPosition = new Vector2Int(x, y);

                // Позиционируем ячейку вручную
                RectTransform cellRect = cell.GetComponent<RectTransform>();
                cellRect.sizeDelta = _cellSize;
                cellRect.anchorMin = new Vector2(0, 1); // Левый верхний угол
                cellRect.anchorMax = new Vector2(0, 1);
                cellRect.pivot = new Vector2(0, 1);

                // Вычисляем позицию с учётом отступов
                float posX = x * (_cellSize.x + _spacing.x);
                float posY = -y * (_cellSize.y + _spacing.y); // Минус, т.к. Y идёт вниз
                cellRect.anchoredPosition = new Vector2(posX, posY);

                _cells[x, y] = cell;
            }
        }
    }

    public void RequestMoveItem(Item item, Vector2Int fromPosition, Vector2Int toPosition)
    {
        OnItemDragged?.Invoke(item, fromPosition, toPosition);
    }

    public void Clear()
    {
        if (_cells == null)
            return;

        foreach (CellView cell in _cells)
        {
            if (cell != null)
                cell.Clear();
        }

        foreach (InventoryItem container in _itemContainers.Values)
        {
            if (container != null)
                Destroy(container.gameObject);
        }

        _itemContainers.Clear();
    }

    private void ClearGrid()
    {
        Clear();

        if (_cells != null)
        {
            foreach (CellView cell in _cells)
            {
                if (cell != null)
                    Destroy(cell.gameObject);
            }
        }

        _cells = null;
    }

    public void DisplayItem(Item item, Vector2Int[] positions)
    {
        if (_cells == null || positions == null || positions.Length == 0)
            return;

        InventoryItem inventoryItem = SpawnInventoryItem(item, positions);

        if (_itemCatalog.GetItemData(item.ItemID, out var data))
            inventoryItem.SetIcon(data.Icon);

        foreach (Vector2Int pos in positions)
        {
            if (pos.x >= 0 && pos.x < _cells.GetLength(0) &&
                pos.y >= 0 && pos.y < _cells.GetLength(1))
            {
                _cells[pos.x, pos.y].SetItem(item, inventoryItem);
            }
        }
    }

    private InventoryItem SpawnInventoryItem(Item item, Vector2Int[] positions)
    {
        // Находим границы предмета
        Vector2Int min = positions[0];
        Vector2Int max = positions[0];

        foreach (Vector2Int p in positions)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
        }

        // ВАЖНО: cols = ширина (по X), rows = высота (по Y)
        int cols = max.x - min.x + 1; // количество колонок (ширина)
        int rows = max.y - min.y + 1; // количество рядов (высота)

        // Создаём контейнер
        InventoryItem inventoryItem = Instantiate(_itemContainerPrefab, _gridContainer);
        RectTransform contRect = inventoryItem.RectTransform;
        contRect.localScale = Vector3.one;
        contRect.pivot = new Vector2(0.5f, 0.5f);

        // Устанавливаем размер контейнера
        // Размер = (количество ячеек * размер ячейки) + (отступы МЕЖДУ ячейками)
        // Отступов между N ячейками = N-1
        float width = cols * _cellSize.x + (cols - 1) * _spacing.x;
        float height = rows * _cellSize.y + (rows - 1) * _spacing.y;
        contRect.sizeDelta = new Vector2(width, height);

        // Позиционируем якорь и pivot как у ячеек
        contRect.anchorMin = new Vector2(0, 1);
        contRect.anchorMax = new Vector2(0, 1);
        contRect.pivot = new Vector2(0, 1);

        // Вычисляем позицию (используем позицию первой ячейки - левый верхний угол)
        RectTransform firstCell = _cells[min.x, min.y].GetComponent<RectTransform>();
        contRect.anchoredPosition = firstCell.anchoredPosition;

        _itemContainers[item] = inventoryItem;
        return inventoryItem;
    }

    private void OnDestroy()
    {
        ClearGrid();
    }
}