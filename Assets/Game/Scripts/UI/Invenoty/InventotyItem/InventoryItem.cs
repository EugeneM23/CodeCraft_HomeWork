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

    private void OnDestroy()
    {
        if (_item != null)
            _item.OnStackChanged -= UpdateQuantity;
    }

    private void UpdateQuantity(int quantity)
    {
        _count.text = quantity.ToString();
    }

    public void SetupItem(ItemInstance item, Vector2 cellSize)
    {
        // Отписываемся от старого item если он был
        if (_item != null)
            _item.OnStackChanged -= UpdateQuantity;

        _item = item;
        _itemImage.sprite = item.itemData.Icon;
        
        // Показываем счётчик только для стакаемых предметов
        bool showCount = item.CanStack;
        _count.gameObject.SetActive(showCount);
        
        if (showCount)
            _count.text = item.StackQuantity.ToString();

        // Подписываемся на изменения
        _item.OnStackChanged += UpdateQuantity;

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