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
        [SerializeField] private BackPack _backPack;
        [SerializeField] private ItemType _itemType;
        [SerializeField] private Image _itemSlot;

        public InventoryItem InventoryItem;

        public bool AddItem(InventoryItem item)
        {
            if (InventoryItem != null)
            {
                if (_backPack.Inventory.AddItem(InventoryItem.Item.itemData))
                {
                    Destroy(InventoryItem.gameObject);
                    PlaceItem(item);
                    return true;
                }

                return false;
            }

            PlaceItem(item);

            return true;
        }

        private void PlaceItem(InventoryItem item)
        {
            InventoryItem = item;
            item.Background.position = transform.position;
            item.transform.SetParent(transform);
        }

        public void RemoveItem()
        {
            _itemSlot.enabled = false;
            InventoryItem = null;
        }
    }
}