using UnityEngine;

public class IdleDragState : BaseState, ITickable
{
    private Vector3 _mouseDownPosition;
    private bool _isMouseDown;
    private readonly float _dragThreshold = 5f;

    public IdleDragState(DragFSM fsm) : base(fsm)
    {
    }

    public void Tick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (_fsm.TryGetSceneRaycastHit(out RaycastHit hit) && hit.collider.GetComponent<SceneItem>())
            {
                _fsm.SetState<StartDragState>();
                return;
            }

            _isMouseDown = true;
            _mouseDownPosition = Input.mousePosition;
        }

        if (_isMouseDown && Input.GetMouseButton(0))
        {
            float dragDistance = Vector3.Distance(_mouseDownPosition, Input.mousePosition);

            if (dragDistance > _dragThreshold)
            {
                _fsm.SetState<StartDragState>();
                _isMouseDown = false;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isMouseDown = false;
        }
    }
}