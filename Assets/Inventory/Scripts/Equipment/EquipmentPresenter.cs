using System.Collections.Generic;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;

namespace Game.Scripts.UI.Equipment.Game.Equipment.Presenter
{
    public class EquipmentPresenter
    {
        private readonly EquipmentPanelView _panelView;
        private readonly Inventory _inventory;

        private readonly Dictionary<ItemType, EquipmentSlotView> _equipmentSlots = new();

        public EquipmentPresenter(EquipmentPanelView panelView, Inventory inventory)
        {
            _inventory = inventory;
            _panelView = panelView;

            _equipmentSlots[ItemType.Head] = _panelView.HeadSlot;
            _equipmentSlots[ItemType.Body] = _panelView.BodySlot;
            _equipmentSlots[ItemType.Legs] = _panelView.LegsSlot;
            _equipmentSlots[ItemType.Boots] = _panelView.BootsSlot;
            _equipmentSlots[ItemType.Hands] = _panelView.HandsSlot;
            _equipmentSlots[ItemType.Weapon] = _panelView.WeaponSlot01;
            _equipmentSlots[ItemType.Shield] = _panelView.WeaponSlot02;
        }

        public void Subscribe()
        {
            _panelView.HeadSlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.HeadSlot.OnDropItemToSlot += DropItemToSlot;

            _panelView.BodySlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.BodySlot.OnDropItemToSlot += DropItemToSlot;

            _panelView.WeaponSlot01.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.WeaponSlot01.OnDropItemToSlot += DropItemToSlot;

            _panelView.WeaponSlot02.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.WeaponSlot02.OnDropItemToSlot += DropItemToSlot;

            _panelView.LegsSlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.LegsSlot.OnDropItemToSlot += DropItemToSlot;

            _panelView.BootsSlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.BootsSlot.OnDropItemToSlot += DropItemToSlot;

            _panelView.HandsSlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.HandsSlot.OnDropItemToSlot += DropItemToSlot;
        }

        public void Unsubscribe()
        {
            _panelView.WeaponSlot01.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.WeaponSlot01.OnDropItemToSlot -= DropItemToSlot;

            _panelView.WeaponSlot02.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.WeaponSlot02.OnDropItemToSlot -= DropItemToSlot;

            _panelView.BodySlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.BodySlot.OnDropItemToSlot -= DropItemToSlot;

            _panelView.HeadSlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.HeadSlot.OnDropItemToSlot -= DropItemToSlot;

            _panelView.LegsSlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.LegsSlot.OnDropItemToSlot -= DropItemToSlot;

            _panelView.BootsSlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.BootsSlot.OnDropItemToSlot -= DropItemToSlot;

            _panelView.HandsSlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.HandsSlot.OnDropItemToSlot -= DropItemToSlot;
        }

        private void DropItemToSlot(ItemInstance itemInstance, EquipmentSlotView slotView)
        {
            if (slotView.CurrentItem != null)
            {
                if (_inventory.AddItem(slotView.CurrentItem.itemData))
                    slotView.EquipItem(itemInstance);

                return;
            }

            slotView.EquipItem(itemInstance);
        }

        private void RemoveItemToInventory(ItemInstance itemInstance, EquipmentSlotView equipmentSlotView)
        {
            if (_inventory.AddItem(itemInstance.itemData))
            {
                equipmentSlotView.UnequipItem();
            }
        }

        public bool EquipToSlot(ItemInstance itemInstance)
        {
            var slot = _equipmentSlots[itemInstance.itemData.ItemType];
            
            if (!slot.IsEmpty)
            {
                ItemInstance currentItem = slot.CurrentItem;
                slot.UnequipItem();
                slot.EquipItem(itemInstance);
                _inventory.AddItem(currentItem.itemData);
                return true;
            }

            slot.EquipItem(itemInstance);
            return true;
        }
    }
}