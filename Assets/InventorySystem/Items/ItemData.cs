using System;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public struct ItemData
    {
        [Header("General")]
        [field: SerializeField]
        public string Name { get;  set; }

        [field: SerializeField] public string Description { get;  set; }
        [field: SerializeField] public ItemType ItemType { get;  set; }

        [Header("Visual")]
        [field: SerializeField]
        public Sprite Icon { get;  set; }

        [field: SerializeField] public Mesh Mesh { get;  set; }

        [Header("Inventory")]
        [field: SerializeField]
        public Vector2Int Size { get;  set; }

        [field: SerializeField] public bool CanStack { get;  set; }
        [field: SerializeField] public int MaxStackQuantity { get;  set; }

        // [Header("Usage")]
        // [field: SerializeField]
        // public ItemUseCase ItemUseCase { get;  set; }

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

            //ItemUseCase = itemData.ItemUseCase;
            
            ItemAudioData = itemData.ItemAudioData;
        }
    }
}