using Inventories;
using UnityEngine;

namespace Game.Scripts.UI.Equipment
{
    public class EquipmentPresenter : MonoBehaviour, IInventoryCollection
    {
        [SerializeField] private Equipment _equipment;

        public void AddItem(Item item, Vector2Int startPosition = default)
        {
            _equipment.AddItem(item, startPosition);
        }

        public void RemoveItem(Item item)
        {
            _equipment.RemoveItem(item);
        }

        public Vector2Int GetItemPosition(Item item) => default;
    }
}