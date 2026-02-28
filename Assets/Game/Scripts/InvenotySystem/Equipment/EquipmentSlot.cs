using Inventories;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class EquipmentSlot : MonoBehaviour
{
    [SerializeField] private ItemType _itemType;
    [SerializeField] private Image _itemIcon;
    [SerializeField] private ItemDragHandler _itemDragHandler;

    public ItemType ItemType => _itemType;
    public Image ItemIcon => _itemIcon;
    public EquipmentPresenter Presenter { get; private set; }
    public ItemDragHandler ItemDragHandler => _itemDragHandler;

    public void Construct(EquipmentPresenter presenter)
    {
        Presenter = presenter;
    }
}