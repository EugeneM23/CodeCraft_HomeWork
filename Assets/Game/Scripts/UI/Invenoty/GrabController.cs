using System.Collections.Generic;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class GrabController : MonoBehaviour
{
    private InventoryPresenter _startPresenter;
    private InventoryPresenter _currentPresenter;

    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private InventoryItem _itemRect;

    private EventSystem _eventSystem;
    private GraphicRaycaster _raycaster;

    private Item _grabItem;
    private Vector2Int _matrixPosition;
    private Vector2Int _startMatrixPosition;
    private InventoryItem _inventoryItem;
    private Vector2 _dragOffset;

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
    }

    private void StartDrag()
    {
        CellView cellView = GetCellUnderMouse();

        if (cellView == null || cellView.Item == null) return;

        _startPresenter = cellView.Presenter;
        _currentPresenter = cellView.Presenter;

        _startMatrixPosition = _startPresenter.GetItemPosition(cellView.Item);
        _grabItem = cellView.Item;
        _matrixPosition = cellView.ItemMatrixPosition;
        _inventoryItem = cellView.InventoryItem;

        //_inventoryItem = CreateInteractItem(_inventoryItem);
        _inventoryItem.EnableBackGround(false);

        _startPresenter.RemoveItemFromInventory(_grabItem);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, Input.mousePosition, null, out Vector2 localPoint);

        _dragOffset = _inventoryItem.RectTransform.anchoredPosition - localPoint;
    }

    private InventoryItem CreateInteractItem(InventoryItem inventoryItem)
    {
        //_currentPresenter.CreateDragItem(_itemRect, inventoryItem.RectTransform.parent);
        
        InventoryItem clone = Instantiate(_itemRect, inventoryItem.RectTransform.parent);

        RectTransform cloneRect = clone.GetComponent<RectTransform>();
        RectTransform sourceRect = _inventoryItem.RectTransform;

        // Копируем параметры
        cloneRect.anchorMin = sourceRect.anchorMin;
        cloneRect.anchorMax = sourceRect.anchorMax;
        cloneRect.pivot = sourceRect.pivot;
        cloneRect.sizeDelta = sourceRect.sizeDelta;
        cloneRect.localScale = sourceRect.localScale * 1.1f;

        // Позицию тоже копируем
        cloneRect.anchoredPosition = sourceRect.anchoredPosition;

        clone.SetIcon(inventoryItem.Icon);

        return clone;
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
                _currentPresenter = currentCell.Presenter;
                Vector2Int currentPosition = currentCell.GridPosition;
                _currentPresenter.HighlightCells(_grabItem, currentPosition, _matrixPosition, currentCell);
            }
            else
            {
                _currentPresenter.ClearHighlights();
            }
        }
    }

    private void EndDrag()
    {
        if (_grabItem != null)
        {
            _inventoryItem.EnableBackGround(true);

            CellView cellView = GetCellUnderMouse();

            if (cellView != null)
            {
                _currentPresenter = cellView.Presenter;

                Vector2Int targetPos = cellView.GridPosition - _matrixPosition;

                Destroy(_inventoryItem.gameObject);
                bool success = _currentPresenter.AddItem(_grabItem, targetPos);

                if (success)
                    _currentPresenter.MoveItem(_grabItem, targetPos);
                else
                    _startPresenter.AddItem(_grabItem, _startMatrixPosition);

                _matrixPosition = Vector2Int.zero;
            }
            else
            {
                Destroy(_inventoryItem.gameObject);
                _startPresenter.AddItem(_grabItem, _startMatrixPosition);
            }

            _grabItem = null;
        }

        _startPresenter.ClearHighlights();
        _currentPresenter.ClearHighlights();
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