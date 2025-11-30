using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Inventories;

public class DragController : MonoBehaviour
{
    [SerializeField] private InventoryView inventoryView;
    [SerializeField] private Canvas canvas;
    [SerializeField] private InventoryPresenter _presenter;

    private GraphicRaycaster raycaster;
    private EventSystem eventSystem;

    private Item draggedItem;
    private Vector2Int dragStartPosition;
    private InventoryItem _inventoryItem;
    private Vector2 dragOffset;
    private Vector2 originalContainerPosition; // Начальная позиция контейнера

    private void Start()
    {
        raycaster = FindObjectOfType<GraphicRaycaster>();
        eventSystem = EventSystem.current;

        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            CellView cell = GetCellUnderMouse();
            if (cell != null && cell.Item != null)
            {
                draggedItem = cell.Item;
                dragStartPosition = cell.GridPosition;
                _inventoryItem = cell.InventoryItem;

                if (_inventoryItem != null)
                {
                    _inventoryItem.transform.SetAsLastSibling();

                    // Сохраняем начальную позицию
                    originalContainerPosition = _inventoryItem.RectTransform.localPosition;

                    // Вычисляем offset между позицией контейнера и курсором
                    Vector2 localPoint;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvas.transform as RectTransform,
                        Input.mousePosition,
                        canvas.worldCamera,
                        out localPoint
                    );

                    dragOffset = (Vector2)_inventoryItem.RectTransform.localPosition - localPoint;
                }
            }
        }

        // Перемещаем контейнер за курсором с учетом offset
        if (_inventoryItem != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                Input.mousePosition,
                canvas.worldCamera,
                out localPoint
            );
            _inventoryItem.RectTransform.localPosition = localPoint + dragOffset;
            _inventoryItem.EnableBackGround(false);
        }

        if (Input.GetMouseButtonUp(0) && draggedItem != null)
        {
            CellView cell = GetCellUnderMouse();
            if (cell != null)
            {
                Vector2Int targetPosition = cell.GridPosition;
                inventoryView.RequestMoveItem(draggedItem, dragStartPosition, targetPosition);
            }
            else
            {
                // Если не попали на ячейку - возвращаем на место
                if (_inventoryItem != null)
                    _inventoryItem.RectTransform.localPosition = originalContainerPosition;
            }

            draggedItem = null;
            _inventoryItem = null;
        }
    }

    private CellView GetCellUnderMouse()
    {
        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.TryGetComponent(out CellView cellView))
                return cellView;
        }

        return null;
    }
}