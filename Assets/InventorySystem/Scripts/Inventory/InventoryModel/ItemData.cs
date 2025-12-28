using System;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public struct ItemData
    {
        [Header("General")]
        [field: SerializeField]
        public string Name { get; private set; }

        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public ItemType ItemType { get; private set; }

        [Header("Visual")]
        [field: SerializeField]
        public Sprite Icon { get; private set; }

        [field: SerializeField] public Mesh Mesh { get; private set; }

        [Header("Inventory")]
        [field: SerializeField]
        public Vector2Int Size { get; private set; }

        [field: SerializeField] public bool CanStack { get; private set; }
        [field: SerializeField] public int MaxStackQuantity { get; private set; }

        [Header("Usage")]
        [field: SerializeField]
        public ItemUseCase ItemUseCase { get; private set; }

        [field: SerializeField] public ItemAudioData ItemAudioData;
        public ItemData(ItemData itemData)
        {
            Name = itemData.Name;
            Description = itemData.Description;
            ItemType = itemData.ItemType;

            Icon = itemData.Icon;
            Mesh = itemData.Mesh;

            Size = itemData.Size;
            CanStack = itemData.CanStack;
            MaxStackQuantity = itemData.MaxStackQuantity;

            ItemUseCase = itemData.ItemUseCase;
            
            ItemAudioData = itemData.ItemAudioData;
        }
    }
}