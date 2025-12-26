using System;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Scripts.UI.Equipment.Game.Equipment.View
{
    public class EquipmentSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<ItemInstance, EquipmentSlotView> OnEquipped;
        public event Action<ItemInstance, EquipmentSlotView> OnUnEquipped;
        public event Action<ItemInstance, EquipmentSlotView> OnRemoveToInventory;
        public event Action<ItemInstance, EquipmentSlotView> OnDropItemToSlot;

        [SerializeField] private Image _itemIcon;
        [FormerlySerializedAs("_allowedItemType")] [SerializeField] private ItemType itemType;
        [SerializeField] private DoubleClickHandler _doubleClick;

        [SerializeField] private Image _background;
        [SerializeField] private Sprite _backgroundEmptySprite;
        [SerializeField] private Sprite _occupiedBackgroundSprite;
        [SerializeField] private Sprite _hoveredBackgroundSprite;

        public ItemType ItemType => itemType;
        public ItemInstance CurrentItem { get; private set; }
        public bool IsEmpty => CurrentItem == null;

        private void OnEnable()
        {
            _doubleClick.OnDoubleClick += HandleDoubleClick;
        }

        private void OnDisable()
        {
            _doubleClick.OnDoubleClick -= HandleDoubleClick;
        }

        private void HandleDoubleClick()
        {
            if (CurrentItem != null)
            {
                OnRemoveToInventory?.Invoke(CurrentItem, this);
            }
        }

        public void DropItemToSlot(ItemInstance itemInstance)
        {
            OnDropItemToSlot?.Invoke(itemInstance, this);
        }

        public bool EquipItem(ItemInstance item)
        {
            if (!CanEquip(item))
                return false;

            CurrentItem = item;
            ShowItem(item.itemData.Icon);

            _background.sprite = _occupiedBackgroundSprite;

            OnEquipped?.Invoke(item, this);
            return true;
        }

        public ItemInstance UnequipItem()
        {
            ItemInstance item = CurrentItem;
            CurrentItem = null;
            ShowEmpty();

            _background.sprite = _backgroundEmptySprite;
            OnUnEquipped?.Invoke(item, this);

            return item;
        }

        private bool CanEquip(ItemInstance item)
        {
            return item?.itemData.ItemType == ItemType;
        }

        private void ShowItem(Sprite icon)
        {
            _itemIcon.enabled = true;
            _itemIcon.sprite = icon;
        }

        private void ShowEmpty()
        {
            _itemIcon.enabled = false;
            _itemIcon.sprite = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _background.sprite = _hoveredBackgroundSprite;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _background.sprite = CurrentItem != null ? _occupiedBackgroundSprite : _backgroundEmptySprite;
        }
    }
}