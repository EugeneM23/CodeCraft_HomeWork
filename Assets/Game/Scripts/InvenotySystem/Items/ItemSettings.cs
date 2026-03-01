using System;
using AudioEngine;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public struct ItemSettings
    {
        [Header("General")]
        [field: SerializeField]
        public string Name { get; set; }

        [field: SerializeField] public string Description { get; set; }
        [field: SerializeField] public ItemType ItemType { get; set; }

        [Header("Visual")]
        [field: SerializeField]
        public Sprite Icon { get; set; }

        [field: SerializeField] public Mesh Mesh { get; set; }

        [Header("Inventory")]
        [field: SerializeField]
        public Vector2Int Size { get; set; }

        [Header("Audio")]
        [field: SerializeField]
        public AudioEventKey StartDrag { get; set; }

        [field: SerializeField] public AudioEventKey AddItemKey { get; set; }
        [field: SerializeField] public AudioEventKey EquipItem { get; set; }
        [field: SerializeField] public AudioEventKey DropToScene { get; set; }
        [field: SerializeField] public AudioEventKey UseItem { get; set; }
        [field: SerializeField] public RuntimeAnimatorController AnimatorController { get; set; }

        public ItemSettings(ItemSettings itemData)
        {
            Name = itemData.Name;
            Description = itemData.Description;
            ItemType = itemData.ItemType;

            Icon = itemData.Icon;
            Mesh = itemData.Mesh;

            Size = itemData.Size;

            StartDrag = itemData.StartDrag;
            AddItemKey = itemData.AddItemKey;
            EquipItem = itemData.EquipItem;
            DropToScene = itemData.DropToScene;
            UseItem = itemData.UseItem;
            AnimatorController = itemData.AnimatorController;
        }
    }
}