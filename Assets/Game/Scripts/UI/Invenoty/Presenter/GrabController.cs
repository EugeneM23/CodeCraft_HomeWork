using System.Collections.Generic;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GrabController : MonoBehaviour
{
    [SerializeField] private InventoryPresenter _presenter;
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private InventoryItem _itemRect;

    private EventSystem _eventSystem;
    private GraphicRaycaster _raycaster;

    private Item _grabItem;
    private Vector2Int _matrixPosition;
    private Vector2Int _startMatrixPosition;
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
    }

    private void StartDrag()
    {
        CellView cellView = GetCellUnderMouse();

        if (cellView == null || cellView.Item == null) return;

        _startMatrixPosition = _presenter.GetPosition(cellView.Item);
        _grabItem = cellView.Item;
        _matrixPosition = cellView.ItemMatrixPosition;
        _inventoryItem = cellView.InventoryItem;

        _inventoryItem = CreateInteractItem(_inventoryItem);
        _inventoryItem.EnableBackGround(false);

        _presenter.RemoveItem(_grabItem);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, Input.mousePosition, null, out Vector2 localPoint);

        _dragOffset = _inventoryItem.RectTransform.anchoredPosition - localPoint;
        _startPosition = _inventoryItem.RectTransform.anchoredPosition;
    }

    private InventoryItem CreateInteractItem(InventoryItem inventoryItem)
    {
        InventoryItem clone = Instantiate(_itemRect, inventoryItem.RectTransform.parent);

        RectTransform cloneRect = clone.GetComponent<RectTransform>();
        RectTransform sourceRect = _inventoryItem.RectTransform;

        // Копируем параметры
        cloneRect.anchorMin = sourceRect.anchorMin;
        cloneRect.anchorMax = sourceRect.anchorMax;
        cloneRect.pivot = sourceRect.pivot;
        cloneRect.sizeDelta = sourceRect.sizeDelta;
        cloneRect.localScale = sourceRect.localScale;

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
                Vector2Int currentPosition = currentCell.GridPosition;
                _presenter.HighlightCells(_grabItem, currentPosition, _matrixPosition, currentCell);
            }
            else
            {
                _presenter.ClearHighlights();
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
                _presenter = cellView.GetComponentInParent<InventoryPresenter>();

                Vector2Int targetPos = cellView.GridPosition - _matrixPosition;

                Destroy(_inventoryItem.gameObject);
                bool success = _presenter.AddItem(_grabItem, targetPos);

                if (success)
                    _presenter.MoveItem(_grabItem, targetPos);
                else
                    _presenter.AddItem(_grabItem, _startMatrixPosition);

                _matrixPosition = Vector2Int.zero;
            }
            else
            {
                Destroy(_inventoryItem.gameObject);
                _presenter.AddItem(_grabItem, _startMatrixPosition);
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