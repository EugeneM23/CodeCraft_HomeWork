using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Inventories
{
    [System.Serializable]
    public class ItemInstance
    {
        public event Action<string> OnStackChanged;
        
        public string uniqueId;
        public ItemData itemData;
        public Vector2Int GridPosition;

        public ItemInstance(ItemData type, Vector2Int gridPosition)
        {
            itemData = type;
            GridPosition = gridPosition;
            uniqueId = System.Guid.NewGuid().ToString();
        }

        public void AddQuantity(int quantity)
        {
            itemData.CurrentStackQuantity += quantity;
            OnStackChanged?.Invoke(itemData.CurrentStackQuantity.ToString());
        }
    }
}