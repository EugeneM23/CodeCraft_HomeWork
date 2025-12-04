using System;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _background;
    [SerializeField] private Image _itemImage;

    public RectTransform RectTransform { get; private set; }
    public Sprite Icon => _itemImage.sprite;
    public Item Item => _item;
    private Item _item;
    private bool _isDragEnable;
    private Vector3 _dragOffset;

    private void Awake() => RectTransform = GetComponent<RectTransform>();

    private void Update()
    {
        if (_isDragEnable)
            transform.position = Input.mousePosition + _dragOffset;
    }

    public void SetItem(Item item) => _item = item;

    public void SetIcon(Sprite sprite) => _itemImage.sprite = sprite;

    public void EnableBackGround(bool enable) => _background.enabled = enable;

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

    public void EnableDrag(bool isEnable, Vector2 offset = default)
    {
        _isDragEnable = isEnable;

        _dragOffset = transform.position - Input.mousePosition;
    }
}