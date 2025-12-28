using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Inventories;
using UnityEngine;

public sealed class Inventory : IEnumerable<Item>
{
    public event Action<Item> OnAdded;
    public event Action<Item> OnRemoved;
    public event Action OnCleared;
    public event Action<Vector2Int[]> OnHighlight;
    public event Action OnUnHighlight;
    public event Action<Item> OnStackIncreased;
    public event Action<Item> OnStackDecreased;
    public event Action<Item, Vector2Int> OnMoved;

    public int Width => _cells.GetLength(0);
    public int Height => _cells.GetLength(1);
    public int Count => _items.Count;
    public IItemConsumer Owner { get; set; }

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

    public Inventory(int width, int height, IEnumerable<KeyValuePair<ItemData, Vector2Int>> items)
        : this(width, height)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        foreach (var kvp in items)
        {
            AddItem(kvp.Key, kvp.Value);
        }
    }

    public Inventory(int width, int height, KeyValuePair<ItemData, Vector2Int>[] items)
        : this(width, height, (IEnumerable<KeyValuePair<ItemData, Vector2Int>>)items)
    {
    }

    public Inventory(int width, int height, IEnumerable<ItemData> items)
        : this(width, height)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        foreach (var itemData in items)
        {
            AddItem(itemData);
        }
    }

    public Inventory(int width, int height, ItemData[] items)
        : this(width, height, (IEnumerable<ItemData>)items)
    {
    }

    #endregion

    #region Add Item Methods

    public bool AddItem(in ItemData itemData, Vector2Int position, int quantity = 1)
    {
        if (itemData.Equals(default(ItemData)))
            return false;

        if (itemData.Size.x <= 0 || itemData.Size.y <= 0)
            throw new ArgumentException("Item size must be positive");
        
        if (Fit(itemData.Size, position.x, position.y))
        {
            var instance = new Item(itemData, position, quantity);
            PlaceInstanceInGrid(instance, position.x, position.y);
            _items.Add(instance.ID, instance);
            OnAdded?.Invoke(instance);
            return true;
        }

        return false;
    }

    public bool AddItem(in ItemData itemData, int x, int y, int quantity = 1)
    {
        return AddItem(itemData, new Vector2Int(x, y), quantity);
    }

    public bool AddItem(in ItemData itemData, int quantity = 1)
    {
        if (itemData.Equals(default(ItemData)))
            return false;

        if (itemData.Size.x <= 0 || itemData.Size.y <= 0)
            throw new ArgumentException("Item size must be positive");

        if (itemData.CanStack)
            if (AddStack(itemData, ref quantity))
                return true;

        if (FindFreePosition(itemData.Size, out var position))
        {
            Item instance = new Item(itemData, position, quantity);
            PlaceInstanceInGrid(instance, position.x, position.y);
            _items.Add(instance.ID, instance);
            OnAdded?.Invoke(instance);
            return true;
        }

        return false;
    }

    private bool AddStack(ItemData itemData, ref int quantity)
    {
        if (!itemData.CanStack || quantity <= 0)
            return false;

        int originalQuantity = quantity;

        foreach (var item in _items.Values)
        {
            // Проверяем, что это тот же предмет
            if (item.itemData.Name != itemData.Name)
                continue;

            // Проверяем, что стак не полон
            if (item.IsFull)
                continue;

            // Пытаемся добавить
            int canAdd = Mathf.Min(quantity, item.RemainingCapacity);

            if (item.TryAddQuantity(canAdd))
            {
                quantity -= canAdd;
                OnStackIncreased?.Invoke(item);

                // Если всё добавили - выходим
                if (quantity == 0)
                    return true;
            }
        }

        // Возвращаем true если хоть что-то добавили к существующим стакам
        // Это позволит создать новый стак для оставшегося количества
        return quantity < originalQuantity;
    }

    #endregion

    #region CanAdd Methods

    public bool CanAddItem(ItemData itemData)
    {
        if (itemData.Equals(default(ItemData)))
            return false;

        if (itemData.Size.x <= 0 || itemData.Size.y <= 0)
            throw new ArgumentException("Item size must be positive");

        return FindFreePosition(itemData.Size, out _);
    }

    public bool CanAddItem(ItemData itemData, Vector2Int position)
    {
        if (itemData.Equals(default(ItemData)))
            return false;

        if (itemData.Size.x <= 0 || itemData.Size.y <= 0)
            throw new ArgumentException("Item size must be positive");

        return Fit(itemData.Size, position.x, position.y);
    }

    #endregion

    #region Remove Item Methods

    public bool RemoveItem(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (!_items.TryGetValue(id, out var item))
            return false;

        Vector2Int[] positions = GetItemGridPositions(item);

        foreach (var position in positions)
            _cells[position.x, position.y] = null;

        _items.Remove(id);
        OnRemoved?.Invoke(item);

        return true;
    }

    #endregion

    #region Move Item Methods

    public bool MoveItem(string itemId, Vector2Int newPosition)
    {
        if (string.IsNullOrEmpty(itemId))
            throw new ArgumentNullException(nameof(itemId));

        if (!_items.TryGetValue(itemId, out var item))
            return false;

        // Проверяем, что новая позиция валидна
        if (!IsPositionValid(item.itemData.Size, newPosition.x, newPosition.y))
            return false;

        // Временно очищаем старую позицию
        var oldPositions = GetItemGridPositions(item);
        foreach (var pos in oldPositions)
            _cells[pos.x, pos.y] = null;

        // Проверяем, что новая позиция свободна
        if (!Fit(item.itemData.Size, newPosition.x, newPosition.y))
        {
            // Восстанавливаем старую позицию
            foreach (var pos in oldPositions)
                _cells[pos.x, pos.y] = item;
            return false;
        }

        // Размещаем на новой позиции
        item.SetGridPosition(newPosition);
        PlaceInstanceInGrid(item, newPosition.x, newPosition.y);

        OnMoved?.Invoke(item, newPosition);
        return true;
    }

    #endregion

    #region Position Methods

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

    #region Get Item Methods

    public Item GetItem(Vector2Int position)
    {
        return GetItem(position.x, position.y);
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
        return TryGetItem(position.x, position.y, out item);
    }

    public bool TryGetItem(int x, int y, out Item item)
    {
        item = null;

        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        item = _cells[x, y];
        return item != null;
    }

    #endregion

    #region Get Positions Methods

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
        ItemData itemData = instance.itemData;
        int cellCount = itemData.Size.x * itemData.Size.y;
        Vector2Int[] positions = new Vector2Int[cellCount];

        int index = 0;
        for (int x = instance.GridPosition.x; x < instance.GridPosition.x + itemData.Size.x; x++)
        {
            for (int y = instance.GridPosition.y; y < instance.GridPosition.y + itemData.Size.y; y++)
            {
                positions[index++] = new Vector2Int(x, y);
            }
        }

        return positions;
    }

    #endregion

    #region Contains Methods

    public bool Contains(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;

        return _items.ContainsKey(itemId);
    }

    #endregion

    #region Count Methods

    public int GetItemCount(string name)
    {
        int count = 0;
        foreach (var item in _items.Values)
        {
            if (item.itemData.Name == name)
                count++;
        }

        return count;
    }

    #endregion

    #region IsFree Methods

    public bool IsFree(Vector2Int cellIndex)
    {
        return IsFree(cellIndex.x, cellIndex.y);
    }

    public bool IsFree(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        return _cells[x, y] == null;
    }

    #endregion

    #region Clear Methods

    public void Clear()
    {
        if (Count == 0)
            return;

        _items.Clear();

        for (int i = 0; i < _cells.GetLength(0); i++)
        for (int j = 0; j < _cells.GetLength(1); j++)
            _cells[i, j] = null;

        OnCleared?.Invoke();
    }

    #endregion

    #region Reorganize Methods

    public void Reorganize()
    {
        if (Count == 0)
            return;

        var itemsToReorganize = new List<(ItemData data, int quantity)>();

        foreach (var item in _items.Values)
        {
            itemsToReorganize.Add((item.itemData, item.StackQuantity));
        }

        itemsToReorganize.Sort((a, b) =>
        {
            if (a.data.CanStack != b.data.CanStack)
                return a.data.CanStack ? -1 : 1;

            int areaA = a.data.Size.x * a.data.Size.y;
            int areaB = b.data.Size.x * b.data.Size.y;

            if (areaA != areaB)
                return areaB.CompareTo(areaA);

            return string.Compare(a.data.Name, b.data.Name, StringComparison.Ordinal);
        });

        _items.Clear();
        for (int i = 0; i < _cells.GetLength(0); i++)
        for (int j = 0; j < _cells.GetLength(1); j++)
            _cells[i, j] = null;

        OnCleared?.Invoke();

        foreach (var (data, quantity) in itemsToReorganize)
        {
            AddItem(data, quantity);
        }
    }

    #endregion

    #region Copy Methods

    public void CopyTo(Item[,] array)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));

        if (array.GetLength(0) != Width || array.GetLength(1) != Height)
            throw new ArgumentException("Array dimensions must match inventory dimensions");

        Array.Copy(_cells, array, _cells.Length);
    }

    #endregion

    #region Helper Methods

    private void PlaceInstanceInGrid(Item instance, int posX, int posY)
    {
        ItemData itemData = instance.itemData;
        for (int x = posX; x < posX + itemData.Size.x; x++)
        {
            for (int y = posY; y < posY + itemData.Size.y; y++)
            {
                _cells[x, y] = instance;
            }
        }
    }

    #endregion

    #region Highlight Methods

    public void UnHighlight() => OnUnHighlight?.Invoke();

    #endregion

    #region IEnumerable Implementation

    public IEnumerator<Item> GetEnumerator()
    {
        foreach ((string key, Item value) in _items)
            yield return value;
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    #endregion
}