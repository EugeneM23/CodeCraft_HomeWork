using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _background;
    [SerializeField] private Image _itemImage;
    [SerializeField] private RectTransform _rectTransform;

    public RectTransform RectTransform => _rectTransform;
    public ItemInstance Item => _item;
    public Sprite Icon => _itemImage.sprite;

    private ItemInstance _item;
    private bool _isDragEnable;
    private Vector3 _dragOffset;

    public void SetItem(ItemInstance item)
    {
        _item = item;
        _itemImage.sprite = item.Item.Icon;

        Vector2 delta = _rectTransform.sizeDelta;
        delta.x *= item.Item.Size.x;
        delta.y *= item.Item.Size.y;
        _rectTransform.sizeDelta = delta;
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Color color = _background.color;
        color.a = 1f;
        _background.color = color;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Color color = _background.color;
        color.a = 0f;
        _background.color = color;
    }
}