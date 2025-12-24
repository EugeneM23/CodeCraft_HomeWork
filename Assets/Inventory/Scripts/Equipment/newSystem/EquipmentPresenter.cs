using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using UnityEngine;

namespace Game.Scripts.UI.Equipment.Game.Equipment.Presenter
{
    public class EquipmentPresenter
    {
        private readonly EquipmentPanelView _panelView;
        private readonly Inventory _inventory;

        public EquipmentPresenter(EquipmentPanelView panelView, Inventory inventory)
        {
            _inventory = inventory;
            _panelView = panelView;
        }

        public void Subscribe()
        {
            _panelView.WeaponSlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.WeaponSlot.OnDropItemToSlot += DropItemToSlot;

            _panelView.ArmorSlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.ArmorSlot.OnDropItemToSlot += DropItemToSlot;
        }
        public void UnSubscribe()
        {
            _panelView.WeaponSlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.ArmorSlot.OnRemoveToInventory -= RemoveItemToInventory;

            _panelView.ArmorSlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.ArmorSlot.OnDropItemToSlot -= DropItemToSlot;
        }


        private void DropItemToSlot(ItemInstance itemInstance, EquipmentSlotView slotView)
        {
            if (slotView.CurrentItem == null)
            {
                slotView.EquipItem(itemInstance);
                return;
            }

            if (slotView.CurrentItem != null && _inventory.AddItem(slotView.CurrentItem.itemData))
                slotView.EquipItem(itemInstance);
        }


        private void RemoveItemToInventory(ItemInstance itemInstance, EquipmentSlotView equipmentSlotView)
        {
            _inventory.AddItem(itemInstance.itemData);
            equipmentSlotView.UnequipItem();
        }

        public bool EquipWeapon(ItemInstance itemInstance)
        {
            if (!_panelView.WeaponSlot.IsEmpty)
            {
                ItemInstance currentWeapon = _panelView.WeaponSlot.CurrentItem;
                _panelView.WeaponSlot.UnequipItem();
                _panelView.WeaponSlot.EquipItem(itemInstance);
                _inventory.AddItem(currentWeapon.itemData);
                return true;
            }

            _panelView.WeaponSlot.UnequipItem();
            _panelView.WeaponSlot.EquipItem(itemInstance);
            return true;
        }

        public bool EquipArmor(ItemInstance itemInstance)
        {
            if (!_panelView.ArmorSlot.IsEmpty)
            {
                ItemInstance currentWeapon = _panelView.ArmorSlot.CurrentItem;
                _panelView.ArmorSlot.UnequipItem();
                _panelView.ArmorSlot.EquipItem(itemInstance);
                _inventory.AddItem(currentWeapon.itemData);
                return true;
            }

            _panelView.ArmorSlot.UnequipItem();
            _panelView.ArmorSlot.EquipItem(itemInstance);
            return true;
        }
    }
}