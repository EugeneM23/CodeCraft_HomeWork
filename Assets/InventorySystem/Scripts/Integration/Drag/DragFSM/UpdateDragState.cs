using UnityEngine;

namespace Inventories
{
    public class UpdateDragState : BaseState, ITickable
    {
        public UpdateDragState(DragFSM fsm) : base(fsm)
        {
        }

        public void Tick()
        {
            UpdateDragItemPosition();
            UpdateCurrentCell();
            UpdateCurrentInventory();

            if (Input.GetMouseButtonUp(0))
                _fsm.SetState<EndDragState>();
        }

        private void UpdateDragItemPosition()
        {
            _fsm.Context.CurrentDragItem.transform.position = Input.mousePosition + _fsm.Context.DragOffset;
        }

        private void UpdateCurrentCell()
        {
            if (!_fsm.TryGetComponentUnderMouse(out CellView hoveredCell)) return;

            _fsm.Context.SelectedCell = hoveredCell.GridPosition - _fsm.Context.GridOffset;
        }

        private void UpdateCurrentInventory()
        {
            if (!_fsm.TryGetComponentUnderMouse(out InventoryBootstrap installer)) return;
            if (_fsm.Context.CurrentInventoryPresenter == installer.Presenter) return;

            _fsm.Context.CurrentInventoryPresenter = installer.Presenter;
            _fsm.Context.CurrentInventoryPresenter = installer.Presenter;
        }
    }
}