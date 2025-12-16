using UnityEngine;

public class UpdateDragState : BaseState, ITickable
{
    private Vector2Int _currentCell;

    public UpdateDragState(DragFSM fsm) : base(fsm)
    {
    }

    public void Tick()
    {
        _fsm.CurrentDragItem.transform.position = Input.mousePosition;

        if (_fsm.TryGetComponentUnderMouse(out CellView cell))
        {
            if (cell.GridPosition != _currentCell)
            {
                _currentCell = cell.GridPosition;
                _fsm.Highlight(_currentCell);
            }
        }

        if (_fsm.TryGetComponentUnderMouse(out BackPack backPack))
        {
            if (_fsm.CurrentInventory != backPack.Inventory)
            {
                if (_fsm.CurrentInventory != null)
                    _fsm.CurrentInventory.UnHighlight();

                _fsm.CurrentInventory = backPack.Inventory;
            }
        }

        if (Input.GetMouseButtonUp(0))
            _fsm.SetState<EndDragState>();
    }
}