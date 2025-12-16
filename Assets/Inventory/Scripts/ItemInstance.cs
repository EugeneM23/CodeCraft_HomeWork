using System;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public class ItemInstance
    {
        public event Action<int> OnStackChanged;

        public string ID { get; private set; }
        public ItemData itemData { get; private set; }
        public Vector2Int GridPosition { get; private set; }
        public ItemUseCase ItemUseCase { get; private set; }
        
        // УДАЛЕНО: ItemConsumer - не нужен!

        private int _stackQuantity;

        public int StackQuantity
        {
            get => _stackQuantity;
            private set
            {
                if (_stackQuantity != value)
                {
                    _stackQuantity = value;
                    OnStackChanged?.Invoke(_stackQuantity);
                }
            }
        }

        public int MaxStackQuantity => itemData.MaxStackQuantity;
        public bool CanStack => itemData.CanStack;
        public bool IsFull => _stackQuantity >= itemData.MaxStackQuantity;
        public int RemainingCapacity => itemData.MaxStackQuantity - _stackQuantity;

        public ItemInstance(ItemData data, Vector2Int gridPosition, int initialQuantity = 1)
        {
            itemData = data;
            GridPosition = gridPosition;
            ID = Guid.NewGuid().ToString();
            _stackQuantity = Mathf.Clamp(initialQuantity, 1, data.CanStack ? data.MaxStackQuantity : 1);
            ItemUseCase = data.ItemUseCase;
            // УДАЛЕНО: ItemConsumer = data.ItemConsumer;
        }

        public bool TryAddQuantity(int quantity)
        {
            if (!CanStack || quantity <= 0)
                return false;

            int newQuantity = _stackQuantity + quantity;

            if (newQuantity > MaxStackQuantity)
                return false;

            StackQuantity = newQuantity;
            return true;
        }

        public bool TryRemoveQuantity(int quantity)
        {
            if (quantity <= 0 || quantity > _stackQuantity)
                return false;

            StackQuantity = _stackQuantity - quantity;
            return true;
        }
    }
}