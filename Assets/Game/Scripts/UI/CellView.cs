using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CellView : MonoBehaviour, IDropHandler
{
    public Image backgroundImage;
    public Sprite normalSprite;
    public Sprite highlightedSprite;

    private void Awake()
    {
        backgroundImage = GetComponent<Image>();
    }

    public void Highlight(Sprite image)
    {
        backgroundImage.sprite = image;
    }

    /*public void Unhighlight()
    {
        backgroundImage.sprite = normalSprite;
    }*/

    public void OnDrop(PointerEventData eventData)
    {
        var item = eventData.pointerDrag;

        if (item != null)
        {
            item.transform.SetParent(transform);
            item.transform.localPosition = Vector3.zero;
        }
    }

    /*public void OnPointerDown(PointerEventData eventData)
    {
        Unhighlight();
    }*/
}