using System.Collections.Generic;
using Game.Scripts.UI.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragController : MonoBehaviour
{
    [SerializeField] private DragItem _dragItemPrefab;
    [SerializeField] private GameObject _pickUpPrefab;
    [SerializeField] private InventoryPresenter _sourcePresentor;
    [SerializeField] private RectTransform _dragArea;

    private GraphicRaycaster _raycaster;
    private EventSystem _eventSystem;

    private DragItem _currentDragItem;
    private bool _isDragging;
    private EquipmentSlot _sourceSlot;
    private WeaponMarker _sourceWeaponMarker; // Сохраняем ссылку на маркер
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

        if (TryDragFromEquipmentSlot())
            return;

        if (TryRaycastUnderMouse(out var hit))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;

            if (hit.collider.gameObject.TryGetComponent(out WeaponMarker marker))
            {
                // НЕ удаляем маркер сразу, только сохраняем ссылку
                _sourceWeaponMarker = marker;

                _currentDragItem = CreateDragItem(this._dragArea.transform, new Vector2(300, 150));

                _currentDragItem.Item = marker.GetItem();
                _currentDragItem.SetIcon(marker.Icon);
                _currentDragItem.Presenter = _sourcePresentor;
                _isDragging = true;
            }
        }
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

        if (IsPointerOverUI())
        {
            ReturnToOriginalPosition();
            return;
        }

        if (TryRaycastUnderMouse(out RaycastHit hit))
        {
            var lookRotation = Quaternion.LookRotation(Vector3.right, hit.normal) * Quaternion.Euler(0, 0, 90);
            Instantiate(_pickUpPrefab, hit.point, lookRotation);
            
            // Удаляем маркер только если успешно создали объект в мире
            if (_sourceWeaponMarker != null)
            {
                Destroy(_sourceWeaponMarker.gameObject);
                _sourceWeaponMarker = null;
            }
            
            Destroy(_currentDragItem.gameObject);
            return;
        }

        ReturnToOriginalPosition();
    }

    private bool IsPointerOverUI()
    {
        PointerEventData pointerData = new(_eventSystem) { position = Input.mousePosition };
        List<RaycastResult> results = new();
        _raycaster.Raycast(pointerData, results);
    
        foreach (RaycastResult result in results)
        {
            if (_currentDragItem != null && result.gameObject.transform.IsChildOf(_currentDragItem.transform))
                continue;
            
            if (result.gameObject == _currentDragItem?.gameObject)
                continue;
            
            return true; 
        }
    
        return false;
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
        _sourceWeaponMarker = null; // Обнуляем, так как тащим из инвентаря

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
        _sourceWeaponMarker = null; // Обнуляем, так как тащим из экипировки
        
        return true;
    }

    private bool TryDropOnInventoryCell()
    {
        if (!TryGetCellUnderMouse(out CellView cell))
            return false;

        // Пытаемся добавить ТОЛЬКО в указанную позицию
        bool added = cell.Presenter.AddItem(_currentDragItem.Item, _currentDragItem.MatrixPosition);

        if (added)
        {
            // Успешно добавили - удаляем маркер если он был
            if (_sourceWeaponMarker != null)
            {
                Destroy(_sourceWeaponMarker.gameObject);
                _sourceWeaponMarker = null;
            }
            
            Destroy(_currentDragItem.gameObject);
            return true;
        }
        
        // Не удалось добавить
        if (_sourceWeaponMarker != null)
        {
            // Если тащили из мира - просто удаляем DragItem, маркер остается
            Destroy(_currentDragItem.gameObject);
            return true;
        }
        
        // Если тащили из инвентаря/экипировки - возвращаем false для возврата на место
        return false;
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

        // Успешно добавили в слот - удаляем маркер если он был
        if (_sourceWeaponMarker != null)
        {
            Destroy(_sourceWeaponMarker.gameObject);
            _sourceWeaponMarker = null;
        }

        Destroy(_currentDragItem.gameObject);
        return true;
    }

    private void ReturnToOriginalPosition()
    {
        // Если тащили из экипировки или инвентаря - возвращаем назад
        if (_currentDragItem.Presenter is EquipmentPresenter)
        {
            _sourceSlot.AddItem(_currentDragItem.Item, _currentDragItem.Icon);
        }
        else if (_sourceWeaponMarker == null) // Если не из мира, значит из инвентаря
        {
            _currentDragItem.Presenter.AddItem(_currentDragItem.Item, _currentDragItem.StartPosition);
        }

        Destroy(_currentDragItem.gameObject);
    }

    private bool TryGetCellUnderMouse(out CellView cellView)
    {
        return TryGetUIComponentUnderMouse(out cellView);
    }

    private bool TryGetSlotUnderMouse(out EquipmentSlot slot)
    {
        return TryGetUIComponentUnderMouse(out slot);
    }

    private bool TryGetUIComponentUnderMouse<T>(out T component) where T : Component
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

    private bool TryRaycastUnderMouse(out RaycastHit hit)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out hit);
    }

    private DragItem CreateDragItem(Transform parent, Vector2 size)
    {
        var item = Instantiate(_dragItemPrefab, parent);
        item.GetComponent<RectTransform>().sizeDelta = size;
        return item;
    }
}