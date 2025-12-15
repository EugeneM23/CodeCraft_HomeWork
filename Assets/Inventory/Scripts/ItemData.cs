using System;
using Inventories;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public struct ItemData
    {
        [field: SerializeField] public string Name;
        [field: SerializeField] public Sprite Icon;
        [field: SerializeField] public Vector2Int Size;
        [field: SerializeField] public ItemType ItemType;

        [field: SerializeField] public bool CanStack;
        [field: SerializeField] public int MaxStackQuantity;

        // Убрали CurrentStackQuantity отсюда - это состояние, а не данные!
        
        public ItemData(ItemData itemData)
        {
            Name = itemData.Name;
            Icon = itemData.Icon;
            Size = itemData.Size;
            ItemType = itemData.ItemType;
            CanStack = itemData.CanStack;
            MaxStackQuantity = itemData.MaxStackQuantity;
        }
    }
}