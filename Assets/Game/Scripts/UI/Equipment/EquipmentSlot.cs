using Inventories;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Scripts.UI.Equipment
{
    public class EquipmentSlot : SerializedMonoBehaviour
    {
        [SerializeField] private BackPack _backPack;
        [SerializeField] private ItemType _itemType;
        [SerializeField] private Image _itemSlot;

        public ItemInstance ItemInstance;

        public bool AddItem(ItemInstance item)
        {
            _itemSlot.enabled = true;
            _itemSlot.sprite = item.itemData.Icon;
            ItemInstance = item;

            // if (ItemInstance != null)
            // {
            //     if (_backPack.Inventory.AddItem(ItemInstance.itemData))
            //     {
            //         _itemSlot.sprite = item.itemData.Icon;
            //         return true;
            //     }
            //
            //     return false;
            // }


            return true;
        }

        public void RemoveItem()
        {
            _itemSlot.enabled = false;
            ItemInstance = null;
            _itemSlot.enabled = false;
        }
    }
}