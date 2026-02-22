using System;
using AudioEngine;
using Inventories;
using Zenject;

namespace Equipment
{
    public class EquipmentSlotAudioController : IInitializable, IDisposable
    {
        private readonly EquipmentSlot[] _slots;
        private AudioSystem _audioSystem;

        public EquipmentSlotAudioController(EquipmentSlot[] slots)
        {
            _slots = slots;
        }

        public void Initialize()
        {
            _audioSystem = AudioSystem.Instance;

            // Подписываемся на события всех слотов
            foreach (var slot in _slots)
            {
                slot.OnEquipped += OnItemEquipped;
                slot.OnUnEquipped += OnItemUnEquipped;
            }
        }

        public void Dispose()
        {
            // Отписываемся от событий всех слотов
            foreach (var slot in _slots)
            {
                slot.OnEquipped -= OnItemEquipped;
                slot.OnUnEquipped -= OnItemUnEquipped;
            }
        }

        private void OnItemEquipped(Item item)
        {
            _audioSystem.PlayEvent(item.Settings.EquipItem);
        }

        private void OnItemUnEquipped(Item item)
        {
            _audioSystem.PlayEvent(item.Settings.StartDrag);
        }
    }
}