using System;
using Inventories;
using Zenject;

namespace Equipment
{
    public class CharacterEquipmentController : IInitializable, IDisposable
    {
        private readonly EquipmentSlot[] _slots;
        private readonly CharacterEquipment _characterEquipment;

        public CharacterEquipmentController(EquipmentSlot[] slots, CharacterEquipment characterEquipment)
        {
            _slots = slots;
            _characterEquipment = characterEquipment;
        }

        public void Initialize()
        {
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
            _characterEquipment.Equip(item);
        }

        private void OnItemUnEquipped(Item item)
        {
            _characterEquipment.Unequip(item);
        }
    }
}