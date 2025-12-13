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

        public InventoryItem InventoryItem;

        public void AddItem(InventoryItem item)
        {
            _addItemAudio.pitch = Random.Range(0.5f, 1.2f);

            _addItemAudio.Play();

            InventoryItem = item;
            item.Background.position = transform.position;
        }

        public void RemoveItem()
        {
            _removeItemAudio.pitch = Random.Range(0.5f, 1.2f);

            _removeItemAudio.Play();
            _itemSlot.enabled = false;
            InventoryItem = null;
        }
    }
}