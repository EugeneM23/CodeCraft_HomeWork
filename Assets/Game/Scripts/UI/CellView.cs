using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CellView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IDropHandler
{
    public Image backgroundImage;
    public Sprite normalSprite;
    public Sprite highlightedSprite;

    private void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
    }

    public void Highlight(Sprite image)
    {
        if (highlightedSprite != null)
            backgroundImage.sprite = image;
    }

    public void Unhighlight()
    {
        if (normalSprite != null)
            backgroundImage.sprite = normalSprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Highlight(highlightedSprite);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Unhighlight();
    }

    public void OnDrop(PointerEventData eventData)
    {
        var item = eventData.pointerDrag;

        if (item != null)
        {
            item.transform.SetParent(transform);
            item.transform.localPosition = Vector3.zero;
        }
    }
}