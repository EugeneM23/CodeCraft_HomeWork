using UnityEngine;

public class UpdateDragState : BaseState, ITickable
{
    private Vector2Int _currentCell;

    public UpdateDragState(DragFSM fsm) : base(fsm)
    {
    }

    public void Tick()
    {
        _fsm.CurrenDragItem.transform.position = Input.mousePosition + _fsm.DragOffset;

        if (_fsm.TryGetComponentUnderMouse(out CellView cell) && cell.GridPosition != _currentCell)
        {
            _currentCell = cell.GridPosition;
            
            // Пересчитываем DragItemCell при каждом наведении на новую ячейку
            // чтобы учесть текущее положение предмета относительно курсора
            _fsm.DragItemCell = _fsm.GetDragItemCell(_fsm.CurrenDragItem, Input.mousePosition);
            
            _fsm.Highlight(_currentCell);
        }

        if (_fsm.TryGetComponentUnderMouse(out BackPack backPack))
        {
            if (_fsm.CurrentInventory != backPack.Inventory)
            {
                _fsm.CurrentInventory?.UnHighlight();
                _fsm.CurrentInventory = backPack.Inventory;
            }
        }

        if (Input.GetMouseButtonUp(0))
            _fsm.SetState<EndDragState>();
    }
}