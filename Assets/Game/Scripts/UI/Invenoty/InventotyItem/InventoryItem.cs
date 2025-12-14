using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _background;
    [SerializeField] private Image _itemImage;
    [SerializeField] private RectTransform _rectTransform;

    private ItemInstance _item;

    public RectTransform RectTransform => _rectTransform;
    public ItemInstance Item => _item;
    public Sprite Icon => _itemImage.sprite;
    public Transform Background => _background.transform;

    public void SetupItem(ItemInstance item, Vector2 cellSize)
    {
        _item = item;
        _itemImage.sprite = item.itemData.Icon;

        Vector2 itemSize = new Vector2(
            cellSize.x * item.itemData.Size.x,
            cellSize.y * item.itemData.Size.y
        );

        _rectTransform.sizeDelta = itemSize;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetBackgroundAlpha(1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetBackgroundAlpha(0f);
    }

    public void DisableBackGround()
    {
        _background.enabled = false;
    }

    private void SetBackgroundAlpha(float alpha)
    {
        Color color = _background.color;
        color.a = alpha;
        _background.color = color;
    }
}