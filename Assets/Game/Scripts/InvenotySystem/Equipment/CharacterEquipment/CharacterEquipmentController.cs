using System;
using Inventories;
using UnityEngine;
using Zenject;

namespace Equipment
{
    public class CharacterEquipmentController : IInitializable, IDisposable
    {
        private readonly EquipmentModel _model;
        private readonly CharacterEquipment _characterEquipment;

        public CharacterEquipmentController(EquipmentModel model, CharacterEquipment characterEquipment)
        {
            _model = model;
            _characterEquipment = characterEquipment;
        }

        public void Initialize()
        {
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
            _characterEquipment.Equip(item);
        }

        private void OnItemUnEquipped(ItemType itemType, Item item)
        {
            _characterEquipment.Unequip(item);
        }
    }
}