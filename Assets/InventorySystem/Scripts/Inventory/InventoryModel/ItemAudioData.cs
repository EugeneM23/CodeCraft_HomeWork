using System;
using UnityEngine;

namespace Inventories
{
    [Serializable]
    public struct ItemAudioData
    {
        [field: SerializeField] public AudioClip StartDrag { get; private set; }
        [field: SerializeField] public AudioClip DropToInventory { get; private set; }
        [field: SerializeField] public AudioClip EquipItem { get; private set; }
        [field: SerializeField] public AudioClip DropToScene { get; private set; }
        [field: SerializeField] public AudioClip UseItem { get; private set; }

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