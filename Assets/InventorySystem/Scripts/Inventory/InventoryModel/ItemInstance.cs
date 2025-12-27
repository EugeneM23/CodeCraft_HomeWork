using System;
using UnityEngine;

namespace Inventories
{
    public class ItemInstance
    {
        public event Action<int> OnStackChanged;

        public string ID { get; private set; }
        public ItemData itemData { get; }
        public Vector2Int GridPosition { get; }
        public int StackQuantity { get; private set; }
        public ItemUseCase ItemUseCase { get; private set; }

        public bool CanStack => itemData.CanStack;
        public bool IsFull => StackQuantity >= itemData.MaxStackQuantity;
        public int RemainingCapacity => itemData.MaxStackQuantity - StackQuantity;

        public ItemInstance(ItemData data, Vector2Int position, int quantity = 1)
        {
            ID = Guid.NewGuid().ToString();
            itemData = data;
            GridPosition = position;
            StackQuantity = Mathf.Clamp(quantity, 1, data.MaxStackQuantity);
            ItemUseCase = data.ItemUseCase;
        }

        public bool TryAddQuantity(int amount)
        {
            if (amount <= 0 || !CanStack || IsFull)
                return false;

            int newQuantity = Mathf.Min(StackQuantity + amount, itemData.MaxStackQuantity);
            int actualAdded = newQuantity - StackQuantity;

            if (actualAdded > 0)
            {
                StackQuantity = newQuantity;
                OnStackChanged?.Invoke(StackQuantity);
                return true;
            }

            return false;
        }

        public bool TryRemoveQuantity(int amount)
        {
            if (amount <= 0)
                return false;

            StackQuantity -= amount;

            if (StackQuantity <= 0)
            {
                StackQuantity = 0;
                return true;
            }

            OnStackChanged?.Invoke(StackQuantity);
            return false;
        }

        public bool UseOne()
        {
            return TryRemoveQuantity(1);
        }
    }
}