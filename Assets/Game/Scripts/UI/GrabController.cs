using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Inventories;

public class DragController : MonoBehaviour
{
    [SerializeField] private InventoryView inventoryView;
    [SerializeField] private Canvas canvas;
    
    private GraphicRaycaster raycaster;
    private EventSystem eventSystem;
    
    private Item draggedItem;
    private Vector2Int dragStartPosition;
    private InventoryItem draggedContainer;
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
                draggedContainer = cell.InventoryItem;
                
                if (draggedContainer != null)
                {
                    draggedContainer.transform.SetAsLastSibling();
                    
                    // Сохраняем начальную позицию
                    originalContainerPosition = draggedContainer.RectTransform.localPosition;
                    
                    // Вычисляем offset между позицией контейнера и курсором
                    Vector2 localPoint;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvas.transform as RectTransform,
                        Input.mousePosition,
                        canvas.worldCamera,
                        out localPoint
                    );
                    
                    dragOffset = (Vector2)draggedContainer.RectTransform.localPosition - localPoint;
                }
            }
        }

        // Перемещаем контейнер за курсором с учетом offset
        if (draggedContainer != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                Input.mousePosition,
                canvas.worldCamera,
                out localPoint
            );
            draggedContainer.RectTransform.localPosition = localPoint + dragOffset;
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
                if (draggedContainer != null)
                    draggedContainer.RectTransform.localPosition = originalContainerPosition;
            }
            
            draggedItem = null;
            draggedContainer = null;
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