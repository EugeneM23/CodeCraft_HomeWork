using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        public int Width => _ceils.GetLength(0);
        public int Height => _ceils.GetLength(1);
        public int Count => _items.Count;

        private List<Item> _items = new();

        public Item[,] _ceils;

        public Inventory(in int width, in int height)
        {
            //Debug.Log("Creating Inventory");
            _ceils = new Item[width, height];
        }

        public Inventory(
            in int width,
            in int height,
            params KeyValuePair<Item, Vector2Int>[] items
        ) : this(width, height)
        {
            foreach ((Item item, Vector2Int vector2Int) in items)
                AddItem(item, vector2Int);
        }

        public Inventory(
            in int width,
            in int height,
            params Item[] items
        ) : this(width, height)
        {
            foreach (var item in items) AddItem(item);
        }

        public Inventory(
            in int width,
            in int height,
            in IEnumerable<KeyValuePair<Item, Vector2Int>> items
        ) : this(width, height)
        {
            foreach ((Item item, Vector2Int vector2Int) in items) AddItem(item, vector2Int);
        }

        public Inventory(
            in int width,
            in int height,
            in IEnumerable<Item> items
        ) : this(width, height)
        {
            foreach (var item in items)
                AddItem(item);
        }

        /// <summary>
        /// Checks for adding an item on a specified position
        /// </summary>
        public bool CanAddItem(in Item item, in Vector2Int position)
        {
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
            freePosition = Vector2Int.zero;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (_ceils[x, y] == null && Fit(size, x, y))
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
                    if (_ceils[x, y] != null)
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
                    _ceils[x, y] = item;
                }
            }
        }

        /// <summary>
        /// Checks if a specified item exists
        /// </summary>
        public bool Contains(in Item item)
        {
            return true;
        }

        /// <summary>
        /// Checks if a specified position is occupied
        /// </summary>
        public bool IsOccupied(in Vector2Int position)
            => throw new NotImplementedException();

        public bool IsOccupied(in int x, in int y)
        {
            return true;
        }

        /// <summary>
        /// Checks if a position is free
        /// </summary>
        public bool IsFree(in Vector2Int position)
        {
            return IsFree(position.x, position.y);
        }

        public bool IsFree(in int x, in int y)
        {
            if (_ceils[x, y] == null)
                return true;

            return false;
        }

        /// <summary>
        /// Removes a specified item if exists
        /// </summary>
        public bool RemoveItem(in Item item)
            => throw new NotImplementedException();

        public bool RemoveItem(in Item item, out Vector2Int position)
            => throw new NotImplementedException();

        /// <summary>
        /// Returns an item at specified position 
        /// </summary>
        public Item GetItem(in Vector2Int position)
            => throw new NotImplementedException();

        public Item GetItem(in int x, in int y)
            => throw new NotImplementedException();

        public bool TryGetItem(in Vector2Int position, out Item item)
            => throw new NotImplementedException();

        public bool TryGetItem(in int x, in int y, out Item item)
            => throw new NotImplementedException();

        /// <summary>
        /// Returns matrix positions of a specified item 
        /// </summary>
        public Vector2Int[] GetPositions(in Item item)
            => throw new NotImplementedException();

        public bool TryGetPositions(in Item item, out Vector2Int[] positions)
            => throw new NotImplementedException();

        /// <summary>
        /// Clears all inventory items
        /// </summary>
        public void Clear()
            => throw new NotImplementedException();

        /// <summary>
        /// Returns a count of items with a specified name
        /// </summary>
        public int GetItemCount(string name)
            => throw new NotImplementedException();

        /// <summary>
        /// Moves a specified item to a target position if it exists
        /// </summary>
        public bool MoveItem(in Item item, in Vector2Int newPosition)
            => throw new NotImplementedException();

        /// <summary>
        /// Reorganizes inventory space to make the free area uniform
        /// </summary>
        public void ReorganizeSpace()
            => throw new NotImplementedException();

        /// <summary>
        /// Copies inventory items to a specified matrix
        /// </summary>
        public void CopyTo(in Item[,] matrix)
            => throw new NotImplementedException();

        public IEnumerator<Item> GetEnumerator()
            => (IEnumerator<Item>)_ceils.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _ceils.GetEnumerator();
    }
}