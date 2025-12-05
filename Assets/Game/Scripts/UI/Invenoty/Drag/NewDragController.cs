using System.Collections.Generic;
using Game.Scripts.UI.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NewDragController : MonoBehaviour
{
    [SerializeField] private DragItem _dragItemPrefab;

    private GraphicRaycaster _raycaster;
    private EventSystem _eventSystem;

    private DragItem _currentDragItem;
    private bool _isDragging;
    private EquipmentSlot _sourceSlot;
    private Vector3 _dragOffset;

    private void Start()
    {
        _raycaster = FindObjectOfType<GraphicRaycaster>();
        _eventSystem = EventSystem.current;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            StartDrag();

        if (Input.GetMouseButton(0) && _isDragging)
            UpdateDrag();

        if (Input.GetMouseButtonUp(0))
            EndDrag();
    }

    private void StartDrag()
    {
        if (TryDragFromInventoryCell())
            return;

        TryDragFromEquipmentSlot();
    }

    private void UpdateDrag()
    {
        _currentDragItem.transform.position = Input.mousePosition;
    }

    private void EndDrag()
    {
        if (!_isDragging)
            return;

        _isDragging = false;

        if (TryDropOnInventoryCell())
            return;

        if (TryDropOnEquipmentSlot())
            return;

        ReturnToOriginalPosition();
    }

    private bool TryDragFromInventoryCell()
    {
        if (!TryGetCellUnderMouse(out CellView cell) || cell.InventoryItem == null)
            return false;

        _currentDragItem = CreateDragItem(cell.transform.parent, cell.InventoryItem.RectTransform.sizeDelta);

        _currentDragItem.Presenter = cell.Presenter;
        _currentDragItem.Item = cell.InventoryItem.Item;
        _currentDragItem.StartPosition = cell.Presenter.GetItemPosition(cell.InventoryItem.Item);
        _currentDragItem.SetIcon(cell.InventoryItem.Icon);

        cell.Presenter.RemoveItem(cell.InventoryItem.Item);

        _dragOffset = default;
        _isDragging = true;

        return true;
    }

    private bool TryDragFromEquipmentSlot()
    {
        if (!TryGetSlotUnderMouse(out EquipmentSlot slot) || slot.CurrentItem == null)
            return false;

        _sourceSlot = slot;
        _currentDragItem = CreateDragItem(slot.transform.parent, slot.GetComponent<RectTransform>().sizeDelta);
        _currentDragItem.SetIcon(slot.Icon);
        _currentDragItem.Item = slot.CurrentItem;
        _currentDragItem.Presenter = slot.Presenter;

        slot.Presenter.RemoveItem(slot.CurrentItem);
        slot.RemoveItem();
        _isDragging = true;
        return true;
    }

    private bool TryDropOnInventoryCell()
    {
        if (!TryGetCellUnderMouse(out CellView cell))
            return false;

        bool added = cell.Presenter.AddItem(_currentDragItem.Item, _currentDragItem.MatrixPosition);

        if (!added)
        {
            ReturnToOriginalPosition();
        }

        Destroy(_currentDragItem.gameObject);
        return true;
    }

    private bool TryDropOnEquipmentSlot()
    {
        if (!TryGetSlotUnderMouse(out EquipmentSlot slot))
            return false;

        if (slot.ItemTipe != _currentDragItem.Item.ItemTipe)
            return false;

        if (slot.CurrentItem != null)
            _currentDragItem.Presenter.AddItem(slot.CurrentItem);

        slot.AddItem(_currentDragItem.Item, _currentDragItem.Icon);
        _sourceSlot = slot;

        Destroy(_currentDragItem.gameObject);
        return true;
    }

    private void ReturnToOriginalPosition()
    {
        if (_currentDragItem.Presenter is EquipmentPresenter)
        {
            _sourceSlot.AddItem(_currentDragItem.Item, _currentDragItem.Icon);
        }
        else
        {
            _currentDragItem.Presenter.AddItem(_currentDragItem.Item, _currentDragItem.StartPosition);
        }

        Destroy(_currentDragItem.gameObject);
    }

    private bool TryGetCellUnderMouse(out CellView cellView)
    {
        return TryGetComponentUnderMouse(out cellView);
    }

    private bool TryGetSlotUnderMouse(out EquipmentSlot slot)
    {
        return TryGetComponentUnderMouse(out slot);
    }

    private bool TryGetComponentUnderMouse<T>(out T component) where T : Component
    {
        component = null;

        PointerEventData pointerData = new(_eventSystem) { position = Input.mousePosition };
        List<RaycastResult> results = new();
        _raycaster.Raycast(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.TryGetComponent(out component))
                return true;
        }

        return false;
    }

    private DragItem CreateDragItem(Transform parent, Vector2 size)
    {
        var item = Instantiate(_dragItemPrefab, parent);
        item.GetComponent<RectTransform>().sizeDelta = size;
        return item;
    }
}