using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragWindow : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private Vector2 offset;
    private RectTransform rectTransform;
    private Canvas canvas;
    private bool _enable;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _enable = true;
        if (eventData.pointerCurrentRaycast.gameObject != this.gameObject)
        {
            _enable = false;
            return;
        }

        gameObject.transform.SetAsLastSibling();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out offset
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject != this.gameObject) return;

        if (!_enable) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 mousePos))
        {
            rectTransform.anchoredPosition = mousePos - offset;
        }
    }
}