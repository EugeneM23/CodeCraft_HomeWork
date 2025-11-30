using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    public event Action<Item, Vector2Int> OnItemMoved;

    [SerializeField] private GridToMatrix _gridToMatrix;
    private CellView[,] _cells;
    private Dictionary<Item, List<Vector2Int>> _itemPositions = new();

    private void Start()
    {
        _cells = _gridToMatrix.BuildMatrix();
        InitializeCells();
    }

    private void InitializeCells()
    {
        for (int x = 0; x < _cells.GetLength(0); x++)
        {
            for (int y = 0; y < _cells.GetLength(1); y++)
            {
                _cells[x, y].GridPosition = new Vector2Int(x, y);
            }
        }
    }

    // Попытка переместить предмет
    public void TryMoveItem(Item item, Vector2Int fromPosition, Vector2Int toPosition)
    {
        if (!_itemPositions.ContainsKey(item)) return;

        // Получаем все позиции предмета относительно его начальной точки
        List<Vector2Int> oldPositions = _itemPositions[item];
        Vector2Int offset = toPosition - fromPosition;

        // Вычисляем новые позиции
        List<Vector2Int> newPositions = new List<Vector2Int>();
        foreach (Vector2Int pos in oldPositions)
        {
            newPositions.Add(pos + offset);
        }

        // Проверяем, можно ли разместить
        if (CanPlaceItem(item, newPositions))
        {
            RemoveItem(item);
            AddItem(item, newPositions.ToArray());
            OnItemMoved?.Invoke(item, toPosition);
        }
    }

    private bool CanPlaceItem(Item itemToPlace, List<Vector2Int> positions)
    {
        foreach (Vector2Int pos in positions)
        {
            // Выход за границы
            if (pos.x < 0 || pos.x >= _cells.GetLength(0) ||
                pos.y < 0 || pos.y >= _cells.GetLength(1))
                return false;

            // Ячейка занята другим предметом
            if (_cells[pos.x, pos.y].Item != null && _cells[pos.x, pos.y].Item != itemToPlace)
                return false;
        }

        return true;
    }

    public void AddItem(Item item, Vector2Int[] positions)
    {
        List<Vector2Int> posList = new List<Vector2Int>(positions);
        _itemPositions[item] = posList;

        foreach (Vector2Int pos in positions)
        {
            _cells[pos.x, pos.y].SetItem(item);
        }
    }

    private void RemoveItem(Item item)
    {
        if (!_itemPositions.ContainsKey(item)) return;

        foreach (Vector2Int pos in _itemPositions[item])
        {
            _cells[pos.x, pos.y].Clear();
        }

        _itemPositions.Remove(item);
    }

    public void Clear()
    {
        foreach (CellView cell in _cells)
        {
            cell.Clear();
        }

        _itemPositions.Clear();
    }
}