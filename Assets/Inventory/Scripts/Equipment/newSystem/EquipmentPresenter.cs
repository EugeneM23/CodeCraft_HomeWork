using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;

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

            _panelView.ItemSlot01.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.ItemSlot01.OnDropItemToSlot += DropItemToSlot;

            _panelView.ItemSlot02.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.ItemSlot02.OnDropItemToSlot += DropItemToSlot;

            _panelView.ItemSlot03.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.ItemSlot03.OnDropItemToSlot += DropItemToSlot;
        }

        public void Unsubscribe()
        {
            _panelView.WeaponSlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.WeaponSlot.OnDropItemToSlot -= DropItemToSlot;

            _panelView.ArmorSlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.ArmorSlot.OnDropItemToSlot -= DropItemToSlot;

            _panelView.ItemSlot01.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.ItemSlot01.OnDropItemToSlot -= DropItemToSlot;

            _panelView.ItemSlot02.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.ItemSlot02.OnDropItemToSlot -= DropItemToSlot;

            _panelView.ItemSlot03.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.ItemSlot03.OnDropItemToSlot -= DropItemToSlot;
        }

        private void DropItemToSlot(ItemInstance itemInstance, EquipmentSlotView slotView)
        {
            if (slotView.CurrentItem != null)
            {
                if (_inventory.AddItem(slotView.CurrentItem.itemData))
                {
                    slotView.EquipItem(itemInstance);
                }

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

        public bool EquipWeapon(ItemInstance itemInstance)
        {
            return EquipToSlot(_panelView.WeaponSlot, itemInstance);
        }

        public bool EquipArmor(ItemInstance itemInstance)
        {
            return EquipToSlot(_panelView.ArmorSlot, itemInstance);
        }

        public bool EquipAmmo(ItemInstance itemInstance)
        {
            return EquipToSlot(_panelView.ItemSlot01, itemInstance);
        }

        private bool EquipToSlot(EquipmentSlotView slot, ItemInstance itemInstance)
        {
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