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

        public event Action<ItemInstance> OnAdded;
        public event Action<ItemInstance> OnRemoved;
        public event Action OnCleared;
        public event Action<Vector2Int[]> OnHighlight;
        public event Action OnUnHighlight;

        private readonly Dictionary<string, ItemInstance> _items;
        private readonly ItemInstance[,] _cells;

        public Inventory(in int width, in int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException();

            _cells = new ItemInstance[width, height];
            _items = new Dictionary<string, ItemInstance>();
        }

        public bool AddItem(in ItemData itemData, Vector2Int position, int quantity = 1)
        {
            if (Fit(itemData.Size, position.x, position.y))
            {
                var instance = new ItemInstance(itemData, position, quantity);
                PlaceInstanceInGrid(instance, position.x, position.y);
                _items.Add(instance.ID, instance);
                OnAdded?.Invoke(instance);
                return true;
            }

            return false;
        }

        public bool AddItem(in ItemData itemData, int quantity = 1)
        {
            // Сначала пытаемся добавить к существующему стаку
            if (TryStackItem(itemData, ref quantity))
            {
                if (quantity == 0)
                    return true;
            }

            // Если осталось что-то добавить - создаём новый стак
            if (quantity > 0 && FindFreePosition(itemData.Size, out var targetPosition))
            {
                ItemInstance instance = new ItemInstance(itemData, targetPosition, quantity);
                PlaceInstanceInGrid(instance, targetPosition.x, targetPosition.y);
                _items.Add(instance.ID, instance);
                OnAdded?.Invoke(instance);
                return true;
            }

            return false;
        }

        private bool TryStackItem(ItemData itemData, ref int quantity)
        {
            if (!itemData.CanStack || quantity <= 0)
                return false;

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
                    
                    // Если всё добавили - выходим
                    if (quantity == 0)
                        return true;
                }
            }

            // Вернём true если хоть что-то добавили
            return quantity < itemData.MaxStackQuantity;
        }

        public bool RemoveItem(string id)
        {
            if (!_items.TryGetValue(id, out var item))
                throw new KeyNotFoundException();

            Vector2Int[] positions = GetItemGridPositions(item);

            foreach (var position in positions)
                _cells[position.x, position.y] = null;

            _items.Remove(id);
            OnRemoved?.Invoke(item);

            return true;
        }

        private bool FindFreePosition(in Vector2Int size, out Vector2Int freePosition)
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

        private void PlaceInstanceInGrid(ItemInstance instance, int posX, int posY)
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

        public Vector2Int[] GetItemGridPositions(ItemInstance instance)
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

        public bool IsFree(Vector2Int cellIndex)
        {
            return _cells[cellIndex.x, cellIndex.y] == null;
        }

        public void Highlight(Vector2Int[] cells) => OnHighlight?.Invoke(cells);

        public void UnHighlight() => OnUnHighlight?.Invoke();
    }
}