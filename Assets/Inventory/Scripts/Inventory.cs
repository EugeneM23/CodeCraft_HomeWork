using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Codice.CM.Common.Purge;
using UnityEditor;
using UnityEngine;

// ReSharper disable NotResolvedInText

namespace Inventories
{
    public sealed class Inventory : IEnumerable<Item>
    {
        public event Action<Item, Vector2Int> OnAdded;
        public event Action<Item, Vector2Int> OnRemoved;
        public event Action<Item, Vector2Int> OnMoved;
        public event Action OnCleared;

        public int Width => _cells.GetLength(0);
        public int Height => _cells.GetLength(1);
        public int Count => _items.Count;

        private List<Item> _items = new();

        public Item[,] _cells;

        public Inventory(in int width, in int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException();

            _cells = new Item[width, height];
        }

        public Inventory(
            in int width,
            in int height,
            params KeyValuePair<Item, Vector2Int>[] items
        ) : this(width, height)
        {
            if (items == null)
                throw new ArgumentNullException();

            foreach ((Item item, Vector2Int vector2Int) in items)
                AddItem(item, vector2Int);
        }

        public Inventory(
            in int width,
            in int height,
            params Item[] items
        ) : this(width, height)
        {
            if (items == null)
                throw new ArgumentNullException();

            foreach (var item in items) AddItem(item);
        }

        public Inventory(
            in int width,
            in int height,
            in IEnumerable<KeyValuePair<Item, Vector2Int>> items
        ) : this(width, height)
        {
            if (items == null)
                throw new ArgumentNullException();

            foreach ((Item item, Vector2Int vector2Int) in items) AddItem(item, vector2Int);
        }

        public Inventory(
            in int width,
            in int height,
            in IEnumerable<Item> items
        ) : this(width, height)
        {
            if (items == null)
                throw new ArgumentNullException();

            foreach (var item in items)
                AddItem(item);
        }

        /// <summary>
        /// Checks for adding an item on a specified position
        /// </summary>
        public bool CanAddItem(in Item item, in Vector2Int position)
        {
            if (item == null)
                return false;

            if (item.Size.x <= 0 || item.Size.y <= 0)
                throw new ArgumentException();
//
            return CanAddItem(item, position.x, position.y);
        }

        public bool CanAddItem(in Item item)
        {
            if (item == null)
                return false;

            if (_items.Contains(item))
                return false;

            return FindFreePosition(item.Size, out _);
        }

        public bool CanAddItem(in Item item, in int posX, in int posY)
        {
            if (item == null)
                return false;

            if (_items.Contains(item))
                return false;

            if (!IsPositionValid(item.Size, posX, posY))
                return false;

            return Fit(item.Size, posX, posY);
        }

        /// <summary>
        /// Adds an item on a specified position if not exists
        /// </summary>
        public bool AddItem(in Item item, in Vector2Int position)
        {
            if (item.Size.x <= 0 || item.Size.y <= 0)
                throw new ArgumentException();

            return AddItem(item, position.x, position.y);
        }

        public bool AddItem(in Item item, in int posX, in int posY)
        {
            if (!CanAddItem(item, posX, posY))
            {
                OnAdded?.Invoke(null, Vector2Int.zero);
                return false;
            }

            PlaceItemInGrid(item, posX, posY);
            OnAdded?.Invoke(item, new Vector2Int(posX, posY));
            _items.Add(item);
            return true;
        }

        /// <summary>
        /// Adds an item on a free position
        /// </summary>
        public bool AddItem(in Item item)
        {
            if (item == null)
                return false;

            if (!FindFreePosition(item.Size, out Vector2Int position))
            {
                OnAdded?.Invoke(null, Vector2Int.zero);
                return false;
            }

            return AddItem(item, position);
        }

        /// <summary>
        /// Returns a free position for a specified item
        /// </summary>
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

        public bool Fit(Vector2Int size, int posX, int posY)
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

        /// <summary>
        /// Validates if the position and size are within grid bounds
        /// </summary>
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

        private void PlaceItemInGrid(Item item, int posX, int posY)
        {
            for (int x = posX; x < posX + item.Size.x; x++)
            {
                for (int y = posY; y < posY + item.Size.y; y++)
                {
                    _cells[x, y] = item;
                }
            }
        }

        /// <summary>
        /// Checks if a specified item exists
        /// </summary>
        public bool Contains(in Item item)
        {
            if (_items.Contains(item))
                return true;

            return false;
        }

        /// <summary>
        /// Checks if a specified position is occupied
        /// </summary>
        public bool IsOccupied(in Vector2Int position)
        {
            return IsOccupied(position.x, position.y);
        }

        public bool IsOccupied(in int x, in int y)
        {
            return _cells[x, y] == null;
        }

        /// <summary>
        /// Checks if a position is free
        /// </summary>
        public bool IsFree(in Vector2Int position)
        {
            return IsFree(position.x, position.y);
        }

        public bool IsFree(in Vector2Int position, Item item)
        {
            if (_cells[position.x, position.y] != null && _cells[position.x, position.y].Equals(item))
                return true;

            return IsFree(position.x, position.y);
        }

        public bool IsFree(in int x, in int y)
        {
            if (_cells[x, y] == null)
                return true;

            return false;
        }

        /// <summary>
        /// Removes a specified item if exists
        /// </summary>
        public bool RemoveItem(in Item item)
        {
            if (_items.Contains(item))
            {
                _items.Remove(item);

                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        if (_cells[x, y] == item)
                            _cells[x, y] = null;
                    }
                }

                OnRemoved?.Invoke(item, Vector2Int.zero);

                return true;
            }

            return false;
        }

        public bool RemoveItem(in Item item, out Vector2Int position)
        {
            if (_items.Contains(item))
            {
                Vector2Int[] vector2Ints = GetPositions(item);
                position = vector2Ints[0];
                RemoveItem(item);
                OnRemoved?.Invoke(item, vector2Ints[0]);
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>
        /// Returns an item at specified position 
        /// </summary>
        public Item GetItem(in Vector2Int position)
        {
            return GetItem(position.x, position.y);
        }

        public Item GetItem(in int x, in int y)
        {
            if (_cells[x, y] == null)
                throw new NullReferenceException();

            return _cells[x, y];
        }

        public bool TryGetItem(in Vector2Int position, out Item item)
        {
            return TryGetItem(position.x, position.y, out item);
        }

        public bool TryGetItem(in int x, in int y, out Item item)
        {
            if (x >= Height || x < 0 || y >= Height || y < 0)
            {
                item = null;
                return false;
            }

            item = _cells[x, y];
            return item != null;
        }

        /// <summary>
        /// Returns matrix positions of a specified item 
        /// </summary>
        public Vector2Int[] GetPositions(in Item item)
        {
            if (item == null)
                throw new NullReferenceException();

            if (!_items.Contains(item))
                throw new KeyNotFoundException();

            List<Vector2Int> positions = new();

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_cells[x, y] == item)
                    positions.Add(new Vector2Int(x, y));

            return positions.ToArray();
        }

        public bool TryGetPositions(in Item item, out Vector2Int[] positions)
        {
            if (_items.Contains(item))
            {
                positions = GetPositions(item);
                return true;
            }

            positions = null;
            return false;
        }

        /// <summary>
        /// Clears all inventory items
        /// </summary>
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

        /// <summary>
        /// Returns a count of items with a specified name
        /// </summary>
        public int GetItemCount(string name)
        {
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Name == name)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Moves a specified item to a target position if it exists
        /// </summary>
        public bool MoveItem(in Item item, in Vector2Int newPosition)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (!_items.Contains(item))
                return false;

            RemoveItemFromGrid(item, out Vector2Int previousPositions);

            if (Fit(item.Size, newPosition.x, newPosition.y))
            {
                PlaceItemInGrid(item, newPosition.x, newPosition.y);
                OnMoved?.Invoke(item, newPosition);
                return true;
            }

            PlaceItemInGrid(item, previousPositions.x, previousPositions.y);
            return false;
        }

        public void RemoveItemFromGrid(Item item, out Vector2Int previousPositions)
        {
            Vector2Int[] positions = GetPositions(item);
            previousPositions = positions[0];

            foreach (Vector2Int pos in positions)
                _cells[pos.x, pos.y] = null;
        }

        /// <summary>
        /// Reorganizes inventory space to make the free area uniform
        /// </summary>
        public void ReorganizeSpace()
        {
            List<Item> allItems = new List<Item>(_items);

            Clear();

            // Сортируем: сначала по площади (убывание), потом по ширине, потом по высоте
            List<Item> sortedList = allItems
                .OrderByDescending(item => item.Size.x * item.Size.y) // площадь
                .ThenByDescending(item => item.Size.x) // ширина
                .ThenByDescending(item => item.Size.y) // высота
                .ToList();

            // Пытаемся разместить каждый предмет
            foreach (var item in sortedList)
            {
                bool placed = false;
                for (int y = 0; y < Height && !placed; y++)
                {
                    for (int x = 0; x < Width && !placed; x++)
                    {
                        if (AddItem(item, new Vector2Int(x, y)))
                        {
                            placed = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Copies inventory items to a specified matrix
        /// </summary>
        public void CopyTo(in Item[,] matrix)
        {
            Array.Copy(_cells, 0, matrix, 0, _cells.Length);
        }

        public IEnumerator<Item> GetEnumerator()
        {
            foreach (Item item in _items)
                yield return item;
        }

        IEnumerator IEnumerable.GetEnumerator()
            => _items.GetEnumerator();
    }
}