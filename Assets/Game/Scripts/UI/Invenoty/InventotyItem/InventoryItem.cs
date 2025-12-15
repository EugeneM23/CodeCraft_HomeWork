using System;
using Inventories;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _background;
    [SerializeField] private Image _itemImage;
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private TMP_Text _count;

    private ItemInstance _item;

    public RectTransform RectTransform => _rectTransform;
    public ItemInstance Item => _item;
    public Transform Background => _background.transform;

    private void Start()
    {
        _count.gameObject.SetActive(_item.itemData.CanStack);
    }

    private void OnDisable() => _item.OnStackChanged -= UpdateQuantity;

    private void UpdateQuantity(string quantity)
    {
        _count.text = quantity;
    }

    public void SetupItem(ItemInstance item, Vector2 cellSize)
    {
        _item = item;
        _itemImage.sprite = item.itemData.Icon;
        _item.OnStackChanged += UpdateQuantity;
        _count.text = item.CurrentQuantity.ToString();

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