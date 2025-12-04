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
    public Item Item => _item;
    private Item _item;
    private bool _isDragEnable;
    private Vector3 _dragOffset;

    private void Update()
    {
        if (_isDragEnable)
            transform.position = Input.mousePosition + _dragOffset;
    }

    public void SetItem(Item item) => _item = item;

    public void SetIcon(Sprite sprite) => _itemImage.sprite = sprite;

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
        _background.enabled = !isEnable;

        transform.SetAsLastSibling();
        _isDragEnable = isEnable;
        _dragOffset = transform.position - Input.mousePosition;
    }
    
    public void SetSize(Vector2 cellSize)
    {
        Vector2 correctSize = new Vector2(
            _item.Size.x * cellSize.x + (_item.Size.x - 1),
            _item.Size.y * cellSize.y + (_item.Size.y - 1)
        );
        _rectTransform.sizeDelta = correctSize;
    }
}