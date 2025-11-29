using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CellView : MonoBehaviour, IDropHandler
{
    public Image backgroundImage;

    private void Awake()
    {
        backgroundImage = GetComponent<Image>();
    }

    public void Highlight(Sprite sprite)
    {
        if (backgroundImage != null && sprite != null)
        {
            backgroundImage.sprite = sprite;
        }
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