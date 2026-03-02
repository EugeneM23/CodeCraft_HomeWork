using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class DraggableWindow : MonoBehaviour, IDragHandler, IBeginDragHandler, IPointerDownHandler
{
    private Vector2 _pointerOffset;
    private bool _isDragging;
    private Canvas _canvas;
    private RectTransform _rectTransform;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _rectTransform = transform as RectTransform;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Проверяем, что raycast попал именно на этот GameObject
        if (!IsPointerOverThisObject(eventData))
        {
            return;
        }

        // Получаем текущую позицию курсора и окна в координатах canvas
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 pointerPos))
        {
            // Offset = где сейчас окно минус где курсор
            _pointerOffset = _rectTransform.anchoredPosition - pointerPos;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Проверяем, что raycast попал именно на этот GameObject
        if (!IsPointerOverThisObject(eventData))
        {
            _isDragging = false;
            return;
        }

        _isDragging = true;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 pointerPos))
        {
            // Новая позиция = где курсор + offset
            _rectTransform.anchoredPosition = pointerPos + _pointerOffset;
        }
    }

    private bool IsPointerOverThisObject(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count > 0)
        {
            return results[0].gameObject == gameObject;
        }

        return false;
    }
}