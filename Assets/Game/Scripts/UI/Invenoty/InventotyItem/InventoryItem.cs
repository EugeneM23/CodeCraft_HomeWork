using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _background;
    [SerializeField] private Image _itemImage;

    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
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