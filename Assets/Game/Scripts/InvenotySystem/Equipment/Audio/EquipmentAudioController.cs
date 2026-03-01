using System;
using AudioEngine;
using Inventories;
using UnityEngine;
using Zenject;

namespace Equipment
{
    public class EquipmentAudioController : IInitializable, IDisposable
    {
        private readonly EquipmentModel _model;
        private AudioSystem _audioSystem;

        public EquipmentAudioController(EquipmentModel model)
        {
            _model = model;
        }

        public void Initialize()
        {
            _audioSystem = AudioSystem.Instance;

            _model.OnEquipped += OnItemEquipped;
            _model.OnUnEquipped += OnItemUnEquipped;
        }

        public void Dispose()
        {
            _model.OnEquipped -= OnItemEquipped;
            _model.OnUnEquipped -= OnItemUnEquipped;
        }

        private void OnItemEquipped(ItemType itemType, Item item)
        {
            _audioSystem.PlayEvent(item.Settings.EquipItem);
        }

        private void OnItemUnEquipped(ItemType itemType, Item item)
        {
            _audioSystem.PlayEvent(InventoryBankAPI.StartDragEvent);
        }
    }
}