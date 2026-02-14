using Game.Scripts.UI.Equipment;
using Game.Scripts.UI.Equipment.Game.Equipment.View;
using Inventories;
using UnityEngine;

public class IdleDragState : BaseState
{
    private Vector3 _mouseDownPosition;
    private bool _isMouseDown;
    private const float DragThreshold = 5f;

    public IdleDragState(DragFSM fsm) : base(fsm)
    {
    }

    public void Tick()
    {
        _fsm.Context.CurrentItemUnderMouse =
            _fsm.TryGetComponentUnderMouse(out CellView cell) ? cell.InventoryItem : null;

        HandleMouseDown();
        HandleMouseDrag();
        HandleMouseUp();
    }

    private void HandleMouseDown()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (TryPickupFromScene())
            return;

        _isMouseDown = true;
        _mouseDownPosition = Input.mousePosition;
    }

    private void HandleMouseDrag()
    {
        if (!_isMouseDown || !Input.GetMouseButton(0)) return;

        if (Vector3.Distance(_mouseDownPosition, Input.mousePosition) > DragThreshold)
        {
            StartDragFromUI();
            _isMouseDown = false;
        }
    }

    private void HandleMouseUp()
    {
        if (Input.GetMouseButtonUp(0))
            _isMouseDown = false;
    }

    private bool TryPickupFromScene()
    {
        if (_fsm.TryGetSceneRaycastHit(out RaycastHit hit) && hit.collider.GetComponent<SceneItem>())
        {
            _fsm.SetState<PickupFromSceneState>();
            return true;
        }

        return false;
    }

    private void StartDragFromUI()
    {
        if (_fsm.TryGetComponentUnderMouse(out EquipmentSlotView slot) && slot.CurrentItem != null)
        {
            Debug.Log("StartDragFromEquipmentState Enter");

            _fsm.SetState<StartDragFromEquipmentState>();
        }
        else if (_fsm.TryGetComponentUnderMouse(out CellView cell) && cell.InventoryItem != null)
        {
            _fsm.SetState<StartDragFromInventoryState>();
        }
    }
}