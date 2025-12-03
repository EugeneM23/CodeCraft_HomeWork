using System.Collections.Generic;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GrabController : MonoBehaviour
{
    [SerializeField] private InventoryPresenter _presenter;
    [SerializeField] private RectTransform _canvasRect;
    private EventSystem _eventSystem;
    private GraphicRaycaster _raycaster;

    private Item _grabItem;
    private Vector2Int _matrixPosition;
    private InventoryItem _inventoryItem;
    private Vector2 _dragOffset;
    private Vector2 _startPosition;

    private void Start()
    {
        _raycaster = FindObjectOfType<GraphicRaycaster>();
        _eventSystem = EventSystem.current;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) StartDrag();
        if (Input.GetMouseButton(0) && _grabItem != null) UpdateDrag();
        if (Input.GetMouseButtonUp(0)) EndDrag();

        Debug.Log(_grabItem == null);
    }

    private void StartDrag()
    {
        CellView cellView = GetCellUnderMouse();

        if (cellView == null || cellView.Item == null) return;

        _presenter = GetComponentInParent<InventoryPresenter>();
        _grabItem = cellView.Item;
        _matrixPosition = cellView.ItemMatrixPosition;
        _inventoryItem = cellView.InventoryItem;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, Input.mousePosition, null, out Vector2 localPoint);

        _dragOffset = _inventoryItem.RectTransform.anchoredPosition - localPoint;
        _startPosition = _inventoryItem.RectTransform.anchoredPosition;

        _presenter.RemoveItem(_grabItem);
    }

    private void UpdateDrag()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, Input.mousePosition, null, out Vector2 localPoint);

        _inventoryItem.RectTransform.anchoredPosition = localPoint + _dragOffset;

        if (_grabItem != null)
        {
            CellView currentCell = GetCellUnderMouse();
            if (currentCell != null)
            {
                _presenter = currentCell.GetComponentInParent<InventoryPresenter>();
                
                Vector2Int currentPosition = currentCell.GridPosition;
                _presenter.HighlightCells(_grabItem, currentPosition, _matrixPosition, currentCell);
            }
        }
    }

    private void EndDrag()
    {
        if (_grabItem != null)
        {
            CellView cellView = GetCellUnderMouse();

            if (cellView != null)
            {
                _presenter = cellView.GetComponentInParent<InventoryPresenter>();

                Vector2Int targetPos = cellView.GridPosition - _matrixPosition;
                _presenter.AddItem(_grabItem, targetPos);
                
                _inventoryItem.transform.parent = _presenter.GridParent;

                if (!_presenter.MoveItem(_grabItem, targetPos))
                    _inventoryItem.RectTransform.anchoredPosition = _startPosition;

                _matrixPosition = Vector2Int.zero;
            }
            else
            {
                _inventoryItem.RectTransform.anchoredPosition = _startPosition;
            }

            _grabItem = null;
        }

        _presenter.ClearHighlights();
    }

    private CellView GetCellUnderMouse()
    {
        PointerEventData pointerData = new(_eventSystem) { position = Input.mousePosition };
        List<RaycastResult> results = new();
        _raycaster.Raycast(pointerData, results);

        foreach (var result in results)
            if (result.gameObject.TryGetComponent(out CellView cell))
                return cell;

        return null;
    }
}