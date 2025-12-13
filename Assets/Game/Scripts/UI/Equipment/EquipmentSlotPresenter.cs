using Inventories;
using UnityEngine;

namespace Game.Scripts.UI.Equipment
{
    public class EquipmentSlotPresenter : MonoBehaviour, IInventoryCollection
    {
        [SerializeField] private EquipmentSlot _equipmentSlot;

        public bool AddItem(ItemData itemData, Vector2Int startPosition = default)
        {
            _equipmentSlot.AddItem(itemData);
            return true;
        }

        public void RemoveItem(string id)
        {
        }

        public Vector2Int GetItemPosition(ItemInstance item)
        {
            return Vector2Int.zero;
        }
    }
}