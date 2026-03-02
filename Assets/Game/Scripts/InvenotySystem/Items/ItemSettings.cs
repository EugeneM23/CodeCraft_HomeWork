using System;
using AudioEngine;
using UnityEngine;
using Object = UnityEngine.Object;

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
        [field: SerializeField] public GameObject ItemPrefab { get; set; }

        public ItemSettings(ItemSettings itemSetings)
        {
            Name = itemSetings.Name;
            Description = itemSetings.Description;
            ItemType = itemSetings.ItemType;

            Icon = itemSetings.Icon;
            Mesh = itemSetings.Mesh;

            Size = itemSetings.Size;

            StartDrag = itemSetings.StartDrag;
            AddItemKey = itemSetings.AddItemKey;
            EquipItem = itemSetings.EquipItem;
            DropToScene = itemSetings.DropToScene;
            UseItem = itemSetings.UseItem;
            AnimatorController = itemSetings.AnimatorController;
            ItemPrefab = itemSetings.ItemPrefab;
        }
    }
}