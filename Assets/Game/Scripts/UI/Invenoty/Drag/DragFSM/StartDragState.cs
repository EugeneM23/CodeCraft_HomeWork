using Game.Scripts.UI.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StartDragState : BaseState
{
    public StartDragState(DragFSM fsm, GraphicRaycaster raycaster, EventSystem eventSystem) : base(fsm, raycaster,
        eventSystem)
    {
    }

    public override void Enter()
    {
        if (_fsm.TryGetComponentUnderMouse<CellView>(out var cell) && cell.InventoryItem != null)
        {
            _fsm.CurrenDragItem = cell.InventoryItem;
            _fsm.DragOffset = _fsm.CurrenDragItem.transform.position - Input.mousePosition;
            _fsm.DragItemCell = _fsm.GetDragItemCell(_fsm.CurrenDragItem, Input.mousePosition);
            _fsm.StartDragCell = cell.GridPosition - _fsm.DragItemCell;
            _fsm.SetState<UpdateDragState>();

            _fsm.RemoveItemFromInventory(_fsm.CurrenDragItem);

            return;
        }

        if (_fsm.TryGetComponentUnderMouse<EquipmentSlot>(out var slot))
        {
            _fsm.CurrenDragItem = slot.;

            _fsm.SetState<UpdateDragState>();
            return;
        }

        if (_fsm.TryGetComponentUnderMouse<SceneItem>(out var sceneItem))
        {
            Debug.Log("Dragging scene");
            return;
        }

        _fsm.SetState<IdleDragState>();
    }
}