using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Inventories;
using UnityEngine;

public sealed class Inventory : IEnumerable<Item>
{
    public event Action<Item, Vector2Int[]> OnAdded;
    public event Action<Item, Vector2Int[]> OnRemoved;
    public event Action OnCleared;
    public event Action OnReorganize;
    public event Action<Item, Vector2Int> OnMoved;

    public int Width => _cells.GetLength(0);
    public int Height => _cells.GetLength(1);
    public int Count => _items.Count;
    public  Dictionary<string, Item> Items => _items;

    private readonly Dictionary<string, Item> _items;
    private readonly Item[,] _cells;

    #region Constructors

    public Inventory(in int width, in int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException();

        _cells = new Item[width, height];
        _items = new Dictionary<string, Item>();
    }

    public Inventory(int width, int height, IEnumerable<KeyValuePair<ItemSettings, Vector2Int>> items)
        : this(width, height)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        foreach (var kvp in items)
        {
            AddItem(kvp.Key, kvp.Value);
        }
    }

    public Inventory(int width, int height, KeyValuePair<ItemSettings, Vector2Int>[] items)
        : this(width, height, (IEnumerable<KeyValuePair<ItemSettings, Vector2Int>>)items)
    {
    }

    public Inventory(int width, int height, IEnumerable<ItemSettings> items)
        : this(width, height)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        foreach (var itemData in items)
        {
            AddItem(itemData);
        }
    }

    public Inventory(int width, int height, ItemSettings[] items)
        : this(width, height, (IEnumerable<ItemSettings>)items)
    {
    }

    #endregion

    #region Add Item

    public bool AddItem(in ItemSettings itemSettings, Vector2Int position, int quantity = 1)
    {
        if (itemSettings.Equals(default(ItemSettings)))
            return false;

        if (itemSettings.Size.x <= 0 || itemSettings.Size.y <= 0)
            throw new ArgumentException("Item size must be positive");

        if (Fit(itemSettings.Size, position.x, position.y))
        {
            Item item = new Item(itemSettings, position);
            PlaceInstanceInGrid(item, position.x, position.y);
            _items.Add(item.ID, item);
            Vector2Int[] positions = GetPositions(item.ID);
            OnAdded?.Invoke(item, positions);
            return true;
        }

        return false;
    }

    public bool AddItem(in ItemSettings itemSettings, int x, int y, int quantity = 1)
    {
        return AddItem(itemSettings, new Vector2Int(x, y), quantity);
    }

    public bool AddItem(in ItemSettings itemSettings, int quantity = 1)
    {
        if (itemSettings.Equals(default(ItemSettings)))
            return false;

        if (itemSettings.Size.x <= 0 || itemSettings.Size.y <= 0)
            throw new ArgumentException("Item size must be positive");

        if (FindFreePosition(itemSettings.Size, out var position))
        {
            Item instance = new Item(itemSettings, position);
            PlaceInstanceInGrid(instance, position.x, position.y);
            _items.Add(instance.ID, instance);
            Vector2Int[] positions = GetItemGridPositions(instance);
            OnAdded?.Invoke(instance, positions);
            return true;
        }

        return false;
    }

    #endregion

    #region CanAdd

    public bool CanAddItem(ItemSettings itemSettings)
    {
        if (itemSettings.Equals(default(ItemSettings)))
            return false;

        if (itemSettings.Size.x <= 0 || itemSettings.Size.y <= 0)
            throw new ArgumentException("Item size must be positive");

        return FindFreePosition(itemSettings.Size, out _);
    }

    public bool CanAddItem(ItemSettings itemSettings, Vector2Int position)
    {
        if (itemSettings.Equals(default(ItemSettings)))
            return false;

        if (itemSettings.Size.x <= 0 || itemSettings.Size.y <= 0)
            throw new ArgumentException("Item size must be positive");

        return Fit(itemSettings.Size, position.x, position.y);
    }

    #endregion

    #region Remove Item

    public bool RemoveItem(string id)
    {
        if (string.IsNullOrEmpty(id) || !_items.TryGetValue(id, out var item))
            return false;

        Vector2Int[] positions = GetItemGridPositions(item);

        foreach (var position in positions)
            _cells[position.x, position.y] = null;

        _items.Remove(id);
        OnRemoved?.Invoke(item, positions);

        return true;
    }

    public bool RemoveItem(Vector2Int position)
    {
        if (position.x < 0 || position.x >= Width || position.y < 0 || position.y >= Height)
            return false;

        var item = _cells[position.x, position.y];

        if (item == null)
            return false;

        // Получаем все позиции, которые занимает этот предмет
        Vector2Int[] positions = GetItemGridPositions(item);

        // Зачищаем все ячейки, выставляя null
        foreach (var pos in positions)
            _cells[pos.x, pos.y] = null;

        // Удаляем из словаря
        _items.Remove(item.ID);

        // Вызываем событие
        OnRemoved?.Invoke(item, positions);

        return true;
    }

    #endregion

    #region Move Item

    public bool MoveItem(string itemId, Vector2Int newPosition)
    {
        if (string.IsNullOrEmpty(itemId))
            throw new ArgumentNullException(nameof(itemId));

        if (!_items.TryGetValue(itemId, out var item))
            return false;

        // Проверяем, что новая позиция валидна
        if (!IsPositionValid(item.Settings.Size, newPosition.x, newPosition.y))
            return false;

        // Проверяем, что новая позиция свободна (игнорируя клетки самого предмета)
        for (int x = newPosition.x; x < newPosition.x + item.Settings.Size.x; x++)
        {
            for (int y = newPosition.y; y < newPosition.y + item.Settings.Size.y; y++)
            {
                var cell = _cells[x, y];
                if (cell != null && cell != item)
                    return false;
            }
        }

        // Все проверки пройдены - выполняем перенос
        // Очищаем старую позицию
        var oldPositions = GetItemGridPositions(item);
        foreach (var pos in oldPositions)
            _cells[pos.x, pos.y] = null;

        // Размещаем на новой позиции
        item.SetGridPosition(newPosition);
        PlaceInstanceInGrid(item, newPosition.x, newPosition.y);

        OnMoved?.Invoke(item, newPosition);
        return true;
    }

    #endregion

    #region Position

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool FindFreePosition(in Vector2Int size, out Vector2Int freePosition)
    {
        if (size.x <= 0 || size.y <= 0)
            throw new ArgumentOutOfRangeException();

        freePosition = Vector2Int.zero;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_cells[x, y] == null && Fit(size, x, y))
                {
                    freePosition = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Fit(Vector2Int size, int posX, int posY)
    {
        if (!IsPositionValid(size, posX, posY))
            return false;

        for (int x = posX; x < posX + size.x; x++)
        {
            for (int y = posY; y < posY + size.y; y++)
            {
                if (_cells[x, y] != null)
                    return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsPositionValid(Vector2Int size, int posX, int posY)
    {
        if (size.x <= 0 || size.y <= 0)
            return false;

        if (posX < 0 || posY < 0)
            return false;

        if (posX + size.x > Width || posY + size.y > Height)
            return false;

        return true;
    }

    #endregion

    #region Get Item

    public Item GetItem(Vector2Int position)
    {
        if (position.x < 0 || position.x >= Width || position.y < 0 || position.y >= Height)
            throw new IndexOutOfRangeException();

        var item = _cells[position.x, position.y];

        if (item == null)
            throw new NullReferenceException();

        return item;
    }

    public Item GetItem(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            throw new IndexOutOfRangeException();

        var item = _cells[x, y];

        if (item == null)
            throw new NullReferenceException();

        return item;
    }

    public bool TryGetItem(Vector2Int position, out Item item)
    {
        item = null;

        if (position.x < 0 || position.x >= Width || position.y < 0 || position.y >= Height)
            return false;

        item = _cells[position.x, position.y];
        return item != null;
    }

    public bool TryGetItem(int x, int y, out Item item)
    {
        item = null;

        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        item = _cells[x, y];
        return item != null;
    }

    public Item GetItem(string id)
    {
        if (_items.TryGetValue(id, out var item))
            return item;

        return null;
    }

    #endregion

    #region Get Positions

    public Vector2Int[] GetPositions(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            throw new NullReferenceException();

        if (!_items.TryGetValue(itemId, out var item))
            throw new KeyNotFoundException();

        return GetItemGridPositions(item);
    }

    public bool TryGetPositions(string itemId, out Vector2Int[] positions)
    {
        positions = null;

        if (string.IsNullOrEmpty(itemId))
            return false;

        if (!_items.TryGetValue(itemId, out var item))
            return false;

        positions = GetItemGridPositions(item);
        return true;
    }

    public Vector2Int[] GetItemGridPositions(Item instance)
    {
        ItemSettings itemSettings = instance.Settings;
        int cellCount = itemSettings.Size.x * itemSettings.Size.y;
        Vector2Int[] positions = new Vector2Int[cellCount];

        int index = 0;
        for (int x = instance.GridPosition.x; x < instance.GridPosition.x + itemSettings.Size.x; x++)
        {
            for (int y = instance.GridPosition.y; y < instance.GridPosition.y + itemSettings.Size.y; y++)
            {
                positions[index++] = new Vector2Int(x, y);
            }
        }

        return positions;
    }

    #endregion

    #region Contains

    public bool Contains(string itemId) => !string.IsNullOrEmpty(itemId) && _items.ContainsKey(itemId);

    #endregion

    #region Count

    public int GetItemCount(string name)
    {
        int count = 0;
        foreach (var item in _items.Values)
        {
            if (item.Settings.Name == name) count++;
        }

        return count;
    }

    #endregion

    #region IsFree

    public bool IsFree(Vector2Int cellIndex)
    {
        return cellIndex.x >= 0 && cellIndex.x < Width && cellIndex.y >= 0 && cellIndex.y < Height &&
               _cells[cellIndex.x, cellIndex.y] == null;
    }

    public bool IsFree(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height && _cells[x, y] == null;
    }

    #endregion

    #region Clear

    public void Clear()
    {
        if (Count == 0)
            return;

        _items.Clear();

        Array.Clear(_cells, 0, _cells.Length);

        OnCleared?.Invoke();
    }

    #endregion

    #region Reorganize

    public void Reorganize()
    {
        if (Count == 0)
            return;

        var itemsToReorganize = new List<ItemSettings>();

        foreach (var item in _items.Values)
        {
            itemsToReorganize.Add(item.Settings);
        }

        itemsToReorganize.Sort((a, b) =>
        {
            int areaA = a.Size.x * a.Size.y;
            int areaB = b.Size.x * b.Size.y;

            if (areaA != areaB)
                return areaB.CompareTo(areaA);

            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        _items.Clear();
        for (int i = 0; i < _cells.GetLength(0); i++)
        for (int j = 0; j < _cells.GetLength(1); j++)
            _cells[i, j] = null;

        foreach (var data in itemsToReorganize)
        {
            AddItem(data);
        }

        OnReorganize?.Invoke();
    }

    #endregion

    #region Copy

    public void CopyTo(Item[,] array)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));

        if (array.GetLength(0) != Width || array.GetLength(1) != Height)
            throw new ArgumentException("Array dimensions must match inventory dimensions");

        Array.Copy(_cells, array, _cells.Length);
    }

    #endregion

    #region Helper

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PlaceInstanceInGrid(Item instance, int posX, int posY)
    {
        ItemSettings itemSettings = instance.Settings;
        for (int x = posX; x < posX + itemSettings.Size.x; x++)
        for (int y = posY; y < posY + itemSettings.Size.y; y++)
            _cells[x, y] = instance;
    }

    #endregion

    #region IEnumerable

    public IEnumerator<Item> GetEnumerator() => _items.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    #endregion
}