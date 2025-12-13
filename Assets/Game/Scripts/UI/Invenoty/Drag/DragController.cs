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
    private SceneItem _sourceSceneItem;
    private Vector3 _dragOffset;

    private void Start()
    {
        _raycaster = FindObjectOfType<GraphicRaycaster>();
        _eventSystem = EventSystem.current;
    }

    private void Update()
    {
        /*if (Input.GetMouseButtonDown(0))
        {
            if (TryGetCellUnderMouse(out var cell) && cell.InventoryItem != null)
            {
                Debug.Log(cell.InventoryItem.Item.uniqueId);
                _sourcePresentor.RemoveItem(cell.InventoryItem.Item.uniqueId);
            }
            Debug.Log(cell.InventoryItem);
        }*/

        Debug.Log(_sourceSceneItem);

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

            if (hit.collider.gameObject.TryGetComponent(out SceneItem sceneItem))
            {
                _sourceSceneItem = sceneItem;

                _currentDragItem = CreateDragItem(_dragArea.transform, new Vector2(300, 150));

                _currentDragItem.ItemData = sceneItem.ItemData;
                _currentDragItem.SetIcon(sceneItem.ItemData.Icon);
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
            SceneItem go = Instantiate(_pickUpPrefab, hit.point, lookRotation).GetComponent<SceneItem>();
            go.ItemData = _currentDragItem.ItemData;

            if (_sourceSceneItem != null)
                Destroy(_sourceSceneItem.gameObject);

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
        _currentDragItem.ItemData = cell.InventoryItem.Item.itemData;
        _currentDragItem.StartPosition = cell.Presenter.GetItemPosition(cell.InventoryItem.Item);
        _currentDragItem.SetIcon(cell.InventoryItem.Icon);

        cell.Presenter.RemoveItem(cell.InventoryItem.Item.uniqueId);

        _dragOffset = default;
        _isDragging = true;
        _sourceSceneItem = null;

        return true;
    }

    private bool TryDragFromEquipmentSlot()
    {
        if (!TryGetSlotUnderMouse(out EquipmentSlot slot) || slot.CurrentItemData == null)
            return false;

        _currentDragItem = CreateDragItem(slot.transform.parent, slot.GetComponent<RectTransform>().sizeDelta);
        _currentDragItem.SetIcon(slot.Icon);
        _currentDragItem.ItemData = slot.CurrentItemData;
        _currentDragItem.Presenter = slot.Presenter;

        slot.RemoveItem();
        _isDragging = true;

        return true;
    }

    private bool TryDropOnInventoryCell()
    {
        if (!TryGetCellUnderMouse(out CellView cell))
            return false;

        bool succes = cell.Presenter.AddItem(_currentDragItem.ItemData, _currentDragItem.MatrixPosition);

        Destroy(_currentDragItem.gameObject);

        if (succes)
        {
            if (_sourceSceneItem != null)
                Destroy(_sourceSceneItem.gameObject);
        }


        return succes;
    }

    private bool TryDropOnEquipmentSlot()
    {
        if (!TryGetSlotUnderMouse(out EquipmentSlot slot))
            return false;

        if (slot.ItemTipe != _currentDragItem.ItemData.ItemType)
            return false;

        if (slot.CurrentItemData != null && _sourceSceneItem != null)
        {
            Destroy(_currentDragItem.gameObject);
            _currentDragItem = null;
            return false;
        }

        if (slot.CurrentItemData != null && _currentDragItem.Presenter != null)
            _currentDragItem.Presenter.AddItem(slot.CurrentItemData);

        slot.AddItem(_currentDragItem.ItemData);

        if (_sourceSceneItem != null)
        {
            Destroy(_sourceSceneItem.gameObject);
            _sourceSceneItem = null;
        }

        Destroy(_currentDragItem.gameObject);
        return true;
    }

    private void ReturnToOriginalPosition()
    {
        if (_currentDragItem == null) return;

        if (_currentDragItem.Presenter != null)
            _currentDragItem.Presenter.AddItem(_currentDragItem.ItemData, _currentDragItem.StartPosition);

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