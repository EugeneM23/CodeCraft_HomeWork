using System;
using Inventories;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.Scripts.UI.Equipment.Game.Equipment.View
{
    public class EquipmentSlotView : MonoBehaviour
    {
        [SerializeField] private Image _itemIcon;
        [SerializeField] private ItemType _itemType;
        [SerializeField] private DoubleClickHandler _doubleClick;
        [SerializeField] private Image _background;
        [SerializeField] private Sprite _emptySprite;
        [SerializeField] private Sprite _occupiedSprite;

        [Inject] private readonly SignalBus _signalBus;

        public ItemType ItemType => _itemType;
        public Item CurrentItem { get; private set; }
        public bool IsEmpty => CurrentItem == null;

        public bool Equip(Item item)
        {
            if (item?.itemData.ItemType != _itemType)
                return false;

            CurrentItem = item;
            _itemIcon.enabled = true;
            _itemIcon.sprite = item.itemData.Icon;
            _background.sprite = _occupiedSprite;

            _signalBus.Fire(new EqipItemAudioSignal
            {
                AudioKey = item.itemData.ItemAudioData.EquipItem,
            });

            return true;
        }

        public Item UnEquip()
        {
            Item item = CurrentItem;
            CurrentItem = null;

            _itemIcon.enabled = false;
            _itemIcon.sprite = null;
            _background.sprite = _emptySprite;

            return item;
        }
    }
}