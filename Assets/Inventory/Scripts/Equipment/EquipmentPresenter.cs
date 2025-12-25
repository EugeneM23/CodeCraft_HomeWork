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

            _panelView.BodySlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.BodySlot.OnDropItemToSlot += DropItemToSlot;

            _panelView.HeadSlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.HeadSlot.OnDropItemToSlot += DropItemToSlot;

            _panelView.LegsSlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.LegsSlot.OnDropItemToSlot += DropItemToSlot;

            _panelView.ItemSlot01.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.ItemSlot01.OnDropItemToSlot += DropItemToSlot;

            _panelView.ItemSlot02.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.ItemSlot02.OnDropItemToSlot += DropItemToSlot;

            _panelView.ItemSlot03.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.ItemSlot03.OnDropItemToSlot += DropItemToSlot;

            _panelView.ShowEquipmentButton.onClick.AddListener(ToggleEquipment);
        }

        public void Unsubscribe()
        {
            _panelView.WeaponSlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.WeaponSlot.OnDropItemToSlot -= DropItemToSlot;

            _panelView.BodySlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.BodySlot.OnDropItemToSlot -= DropItemToSlot;

            _panelView.HeadSlot.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.HeadSlot.OnDropItemToSlot -= DropItemToSlot;

            _panelView.LegsSlot.OnRemoveToInventory += RemoveItemToInventory;
            _panelView.LegsSlot.OnDropItemToSlot += DropItemToSlot;

            _panelView.ItemSlot01.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.ItemSlot01.OnDropItemToSlot -= DropItemToSlot;

            _panelView.ItemSlot02.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.ItemSlot02.OnDropItemToSlot -= DropItemToSlot;

            _panelView.ItemSlot03.OnRemoveToInventory -= RemoveItemToInventory;
            _panelView.ItemSlot03.OnDropItemToSlot -= DropItemToSlot;

            _panelView.ShowEquipmentButton.onClick.RemoveListener(ToggleEquipment);
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
            return EquipToSlot(_panelView.BodySlot, itemInstance);
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

        private void ToggleEquipment()
        {
            _panelView.gameObject.SetActive(!_panelView.gameObject.activeSelf);
        }
    }
}