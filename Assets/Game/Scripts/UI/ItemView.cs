using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;

public class ItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Transform originalParent;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private GridToMatrix gridMatrix;

    private Item _modelItem;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        gridMatrix = FindObjectOfType<GridToMatrix>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        transform.SetParent(canvas.transform);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pos);

        transform.localPosition = pos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        if (eventData.pointerEnter != null)
        {
            CellView cell = eventData.pointerEnter.GetComponent<CellView>();
            if (cell != null && gridMatrix.TryGetCellPosition(cell, out int row, out int col))
            {
                // Пытаемся переместить предмет через презентор
                //bool moved = presenter.TryMoveItem(this, new Vector2Int(row, col));
                // if (moved)
                // {
                //     return;
                // }
            }
        }

        // Возвращаемся на исходную позицию
        transform.SetParent(originalParent);
        transform.localPosition = Vector3.zero;
        transform.SetAsLastSibling(); // Убеждаемся что предмет рисуется поверх клеток
    }
}