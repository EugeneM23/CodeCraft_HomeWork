using Inventories;
using UnityEngine;
using Zenject;

public class EquipmentSlotMarker : MonoBehaviour
{
    [SerializeField] private ItemType _itemType;

    public ItemType ItemType => _itemType;
    [Inject] public EquipmentPresenter Presenter { get; set; }
}