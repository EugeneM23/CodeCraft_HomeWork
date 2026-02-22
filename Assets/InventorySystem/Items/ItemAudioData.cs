using System;
using AudioEngine;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public struct ItemAudioData
    {
        [field: SerializeField] public AudioEventKey StartDrag { get; private set; }
        [field: SerializeField] public AudioEventKey DropToInventory { get; private set; }
        [field: SerializeField] public AudioEventKey EquipItem { get; private set; }
        [field: SerializeField] public AudioEventKey DropToScene { get; private set; }
        [field: SerializeField] public AudioEventKey UseItem { get; private set; }

        public ItemAudioData(ItemAudioData itemData)
        {
            StartDrag = itemData.StartDrag;
            DropToInventory = itemData.DropToInventory;
            DropToScene = itemData.DropToScene;
            EquipItem = itemData.EquipItem;
            UseItem = itemData.UseItem;
        }
    }
}