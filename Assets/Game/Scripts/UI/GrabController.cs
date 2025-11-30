using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Inventories;

public class GrabController : MonoBehaviour
{
    [SerializeField] private InventoryView inventoryView;
    private GraphicRaycaster raycaster;
    private EventSystem eventSystem;
    
    private Item draggedItem;
    private Vector2Int dragStartPosition;

    private void Start()
    {
        raycaster = FindObjectOfType<GraphicRaycaster>();
        eventSystem = EventSystem.current;
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
            }
        }

        if (Input.GetMouseButtonUp(0) && draggedItem != null)
        {
            CellView cell = GetCellUnderMouse();
            if (cell != null)
            {
                Vector2Int targetPosition = cell.GridPosition;
                inventoryView.TryMoveItem(draggedItem, dragStartPosition, targetPosition);
            }
            
            draggedItem = null;
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