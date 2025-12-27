using System;
using Inventories;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public struct ItemData
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public Vector2Int Size { get; private set; }
        [field: SerializeField] public ItemType ItemType { get; private set; }
        [field: SerializeField] public bool CanStack { get; private set; }
        [field: SerializeField] public int MaxStackQuantity { get; private set; }
        [field: SerializeField] public ItemUseCase ItemUseCase { get; private set; }
        [field: SerializeField] public Mesh Mesh { get; private set; }
        [field: SerializeField] public string Discription { get; private set; }

        public ItemData(ItemData itemData)
        {
            Name = itemData.Name;
            Icon = itemData.Icon;
            Size = itemData.Size;
            ItemType = itemData.ItemType;
            CanStack = itemData.CanStack;
            MaxStackQuantity = itemData.MaxStackQuantity;
            ItemUseCase = itemData.ItemUseCase;
            Mesh = itemData.Mesh;
            Discription = itemData.Discription;
        }
    }
}