using Inventories;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
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
        public ItemData CurrentItemData { get; set; }

        private ItemData _itemData;

        public void AddItem(ItemData itemData, Sprite icon)
        {
            _addItemAudio.pitch = Random.Range(0.5f, 1.2f);

            _addItemAudio.Play();

            CurrentItemData = itemData;
            _itemSlot.enabled = true;
            _itemData = itemData;
            _itemSlot.sprite = icon;
        }

        public void RemoveItem()
        {
            _removeItemAudio.pitch = Random.Range(0.5f, 1.2f);

            _removeItemAudio.Play();
            CurrentItemData = null;
            _itemSlot.enabled = false;
        }
    }
}