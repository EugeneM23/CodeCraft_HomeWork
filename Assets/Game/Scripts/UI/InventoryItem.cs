using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _background;
    [SerializeField] private Image _itemImage;
    private Image _image;

    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        _image = GetComponent<Image>();
    }

    public void SetIcon(Sprite sprite)
    {
        _itemImage.sprite = sprite;
    }

    public void EnableBackGround(bool enable)
    {
        _background.enabled = enable;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Color color = _background.color;
        color.a = 1f; // вместо 255
        _background.color = color; // присваиваем обратно
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Color color = _background.color;
        color.a = 0f; // полностью прозрачный
        _background.color = color; // присваиваем обратно
    }
}