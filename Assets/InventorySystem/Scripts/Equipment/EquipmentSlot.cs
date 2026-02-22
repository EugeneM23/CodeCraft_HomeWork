using Inventories;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Equipment
{
    public class EquipmentSlot : MonoBehaviour
    {
        [SerializeField] private ItemType _itemType;
        [SerializeField] private Image _itemIcon;

        [Inject] private readonly EquipmentPresenter _presenter;

        private Item _currentItem;
        public bool IsEmpty => _currentItem == null;
        public ItemType ItemType => _itemType;
        public EquipmentPresenter Presenter => _presenter;

        public void Equip(Item item)
        {
            _currentItem = item;
            _itemIcon.enabled = true;
            _itemIcon.sprite = item.Settings.Icon;
        }

        public Item UnEquip()
        {
            var item = _currentItem;
            _currentItem = null;
            _itemIcon.enabled = false;
            _itemIcon.sprite = null;

            return item;
        }
    }
}