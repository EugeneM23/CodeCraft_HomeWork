using Inventories;
using UnityEngine;

public class EquipmentSlotMarker : MonoBehaviour
{
    [SerializeField] private ItemType _itemType;

    public ItemType ItemType => _itemType;
}