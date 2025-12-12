using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Inventories
{
    public sealed class Inventory : IEnumerable<ItemInstance>
    {
        public int Width => _cells.GetLength(0);
        public int Height => _cells.GetLength(1);
        public int Count => _items.Count;

        public event Action<ItemInstance, Vector2Int> OnAdded;
        public event Action<ItemInstance, Vector2Int> OnRemoved;
        public event Action OnCleared;

        private readonly Dictionary<string, ItemInstance> _items;
        public ItemInstance[,] _cells;

        public Inventory(in int width, in int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException();

            _cells = new ItemInstance[width, height];
            _items = new Dictionary<string, ItemInstance>();
        }

        public ItemInstance AddItem(in Item item, Vector2Int? position = null)
        {
            Vector2Int targetPosition;
            if (position.HasValue)
            {
                targetPosition = position.Value;
            }
            else
            {
                if (!FindFreePosition(item.Size, out targetPosition))
                    return null;
            }

            item.GridPosition = targetPosition;
            ItemInstance instance = new ItemInstance(item);

            PlaceInstanceInGrid(instance, targetPosition.x, targetPosition.y);
            _items.Add(instance.uniqueId, instance);

            OnAdded?.Invoke(instance, targetPosition);

            return instance;
        }

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

        private void PlaceInstanceInGrid(ItemInstance instance, int posX, int posY)
        {
            Item item = instance.Item;
            for (int x = posX; x < posX + item.Size.x; x++)
            {
                for (int y = posY; y < posY + item.Size.y; y++)
                {
                    _cells[x, y] = instance;
                }
            }
        }

        public bool RemoveInstance(string id)
        {
            if (!_items.ContainsKey(id))
                return false;

            _items.Remove(id);

            return true;
        }

        public Vector2Int[] GetItemGridPositions(Item item)
        {
            int cellCount = item.Size.x * item.Size.y;
            Vector2Int[] positions = new Vector2Int[cellCount];

            int index = 0;
            for (int x = item.GridPosition.x; x < item.GridPosition.x + item.Size.x; x++)
            {
                for (int y = item.GridPosition.y; y < item.GridPosition.y + item.Size.y; y++)
                {
                    positions[index] = new Vector2Int(x, y);
                    index++;
                }
            }

            return positions;
        }

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

        public IEnumerator<ItemInstance> GetEnumerator()
        {
            foreach ((string key, ItemInstance value) in _items)
                yield return value;
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}