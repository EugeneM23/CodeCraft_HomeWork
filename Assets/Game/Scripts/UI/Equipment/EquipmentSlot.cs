using Inventories;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Scripts.UI.Equipment
{
    public class EquipmentSlot : SerializedMonoBehaviour
    {
        [OdinSerialize] private IInventoryCollection _presenter;
        [SerializeField] private ItemType _itemType;
        [SerializeField] private Image _itemSlot;
        [SerializeField] private AudioSource _addItemAudio;
        [SerializeField] private AudioSource _removeItemAudio;

        public IInventoryCollection Presenter => _presenter;
        public Sprite Icon => _itemSlot.sprite;
        public ItemType ItemTipe => _itemType;
        public Item CurrentItem { get; set; }

        private Item _item;

        public void AddItem(Item item, Sprite icon)
        {
            _addItemAudio.Play();

            CurrentItem = item;
            _itemSlot.enabled = true;
            _item = item;
            _itemSlot.sprite = icon;
        }

        public void RemoveItem()
        {
            _removeItemAudio.Play();
            CurrentItem = null;
            _itemSlot.enabled = false;
        }
    }
}