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
    [SerializeField] private DoubleClickHandler _doubleClickHandler;

    public ItemInstance Item { get; private set; }
    public RectTransform RectTransform => _rectTransform;

    public Transform Background => _background.transform;

    private void OnDestroy()
    {
        if (Item != null)
            Item.OnStackChanged -= UpdateQuantity;
    }

    private void UpdateQuantity(int quantity)
    {
        _count.text = quantity.ToString();
    }

    public void SetupItem(ItemInstance item, Vector2 cellSize)
    {
        // Отписываемся от старого item если он был
        if (Item != null)
            Item.OnStackChanged -= UpdateQuantity;

        Item = item;
        _itemImage.sprite = item.itemData.Icon;

        // Показываем счётчик только для стакаемых предметов
        bool showCount = item.CanStack;
        _count.gameObject.SetActive(showCount);

        if (showCount)
            _count.text = item.StackQuantity.ToString();

        // Подписываемся на изменения
        Item.OnStackChanged += UpdateQuantity;

        Vector2 itemSize = new Vector2(
            cellSize.x * item.itemData.Size.x,
            cellSize.y * item.itemData.Size.y
        );

        _rectTransform.sizeDelta = itemSize;

        Debug.Log(item.ItemConsumer == null);
        _doubleClickHandler.SetUpUseCase(item.ItemUseCase, item.ItemConsumer);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _itemImage.transform.localScale *= 1.2f;
        SetBackgroundAlpha(1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _itemImage.transform.localScale = Vector3.one;

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