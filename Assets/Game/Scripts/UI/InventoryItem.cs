using UnityEngine;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour
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
}