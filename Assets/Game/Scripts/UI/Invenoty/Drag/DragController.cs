// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.EventSystems;
// using UnityEngine.UI;
//
// public class DragController : MonoBehaviour
// {
//     [SerializeField] private RectTransform _parent;
//
//     private InventoryPresenter _currentPresenter;
//     private EventSystem _eventSystem;
//     private GraphicRaycaster _raycaster;
//     private InventoryItem _draggedInventoryItem;
//     private bool _isDragging;
//
//     private void Start()
//     {
//         _raycaster = FindObjectOfType<GraphicRaycaster>();
//         _eventSystem = EventSystem.current;
//     }
//
//     private void Update()
//     {
//         if (Input.GetMouseButtonDown(0)) StartDrag();
//         if (Input.GetMouseButton(0) && _isDragging) UpdateDrag();
//         if (Input.GetMouseButtonUp(0)) EndDrag();
//     }
//
//     private void StartDrag()
//     {
//         CellView cell = GetCellUnderMouse();
//
//         if (cell == null || cell.InventoryItem == null) return;
//
//         InitializeDragState(cell);
//         _currentPresenter.RemoveFromModelItem(_draggedInventoryItem.Item);
//         _draggedInventoryItem.EnableDrag(true);
//     }
//
//     private void UpdateDrag()
//     {
//         CellView cell = GetCellUnderMouse();
//
//         if (cell != null)
//         {
//             UpdateCurrentPresenter(cell);
//             _currentPresenter.HighlightCells(
//                 _draggedInventoryItem.Item,
//                 cell.GridPosition,
//                 _draggedInventoryItem.GetSavedMatrixPosition(),
//                 cell
//             );
//         }
//         else
//         {
//             _currentPresenter?.ClearHighlights();
//         }
//     }
//
//     private void EndDrag()
//     {
//         if (!_isDragging) return;
//
//         _draggedInventoryItem.EnableDrag(false);
//         _isDragging = false;
//
//         CellView cell = GetCellUnderMouse();
//         TryPlaceItem(cell);
//
//         ClearAllHighlights();
//         _draggedInventoryItem = null;
//     }
//
//     private void InitializeDragState(CellView cell)
//     {
//         _isDragging = true;
//         _currentPresenter = cell.Presenter;
//         _draggedInventoryItem = cell.InventoryItem;
//         
//         _draggedInventoryItem.SaveState(
//             _draggedInventoryItem.transform.parent,
//             _draggedInventoryItem.transform.position,
//             _currentPresenter.GetItemPosition(_draggedInventoryItem.Item),
//             cell.ItemMatrixPosition
//         );
//     }
//
//     private void UpdateCurrentPresenter(CellView cell)
//     {
//         if (cell.Presenter != _currentPresenter)
//         {
//             _currentPresenter = cell.Presenter;
//             _draggedInventoryItem.transform.SetParent(_currentPresenter.transform);
//         }
//     }
//
//     private void TryPlaceItem(CellView targetCell)
//     {
//         Vector2Int targetPosition;
//         InventoryPresenter targetPresenter;
//
//         if (targetCell != null)
//         {
//             targetPosition = targetCell.GridPosition - _draggedInventoryItem.GetSavedMatrixPosition();
//             targetPresenter = targetCell.Presenter;
//         }
//         else
//         {
//             Transform savedParent = _draggedInventoryItem.GetSavedParent();
//             targetPresenter = savedParent?.GetComponentInParent<InventoryPresenter>();
//             if (targetPresenter == null) return;
//             targetPosition = _draggedInventoryItem.GetSavedGridPosition();
//         }
//
//         bool placed = targetPresenter.MoveItem(
//             _draggedInventoryItem.Item,
//             _draggedInventoryItem,
//             targetPosition
//         );
//
//         if (!placed)
//         {
//             _draggedInventoryItem.RestoreState();
//             Transform savedParent = _draggedInventoryItem.GetSavedParent();
//             InventoryPresenter savedPresenter = savedParent?.GetComponentInParent<InventoryPresenter>();
//             savedPresenter?.MoveItem(
//                 _draggedInventoryItem.Item,
//                 _draggedInventoryItem,
//                 _draggedInventoryItem.GetSavedGridPosition()
//             );
//         }
//         else
//         {
//             _draggedInventoryItem.ClearSavedState();
//             Transform savedParent = _draggedInventoryItem.GetSavedParent();
//             InventoryPresenter savedPresenter = savedParent?.GetComponentInParent<InventoryPresenter>();
//             
//             if (savedPresenter != null && savedPresenter != targetPresenter)
//             {
//                 savedPresenter.RemoveFromViewItem(_draggedInventoryItem);
//                 targetPresenter.AddViewItemToView(_draggedInventoryItem);
//             }
//         }
//     }
//
//     private void ClearAllHighlights()
//     {
//         _currentPresenter?.ClearHighlights();
//         Transform savedParent = _draggedInventoryItem?.GetSavedParent();
//         savedParent?.GetComponentInParent<InventoryPresenter>()?.ClearHighlights();
//     }
//
//     private CellView GetCellUnderMouse()
//     {
//         PointerEventData pointerData = new(_eventSystem) { position = Input.mousePosition };
//         List<RaycastResult> results = new();
//         _raycaster.Raycast(pointerData, results);
//
//         foreach (RaycastResult result in results)
//         {
//             if (result.gameObject.TryGetComponent(out CellView cell))
//                 return cell;
//         }
//
//         return null;
//     }
// }