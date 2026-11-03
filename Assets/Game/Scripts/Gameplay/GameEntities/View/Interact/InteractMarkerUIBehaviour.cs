using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class InteractMarkerUIBehaviour : IEntityInit, IEntityDispose, IEntityLateTick
    {
        private readonly GameObject _view;
        private readonly GameContext _gameContext;
        private IReactiveVariable<IEntity> _targetInteractable;
        private Vector3 _positionOffset;
        private Transform _cameraTransform;

        public InteractMarkerUIBehaviour(GameObject view, GameContext gameContext)
        {
            _gameContext = gameContext;
            _view = view;
        }

        public void Init(IEntity entity)
        {
            _targetInteractable = _gameContext.GetPlayerContext().GetCharacter().Value.GetTargetInteractable();
            _cameraTransform = _gameContext.GetPlayerContext().GetCamera().GetTransform();
            _targetInteractable.Subscribe(OnTargetChanged);
            _positionOffset = entity.GetPositionOffset();
        }

        public void Dispose(IEntity entity)
        {
            _targetInteractable.Unsubscribe(OnTargetChanged);
        }

        private void OnTargetChanged(IEntity item)
        {
            if (_targetInteractable.Value != null)
            {
                _view.SetActive(true);
                Transform transform = item.GetTransform();
                _view.transform.position = transform.position + _positionOffset;
            }
            else
            {
                _view.SetActive(false);
            }
        }

        public void LateTick(IEntity entity, float deltaTime)
        {
            if (!_view.activeSelf)
                return;

            // The marker should only yaw towards the camera, so the direction is flattened before
            // building the rotation. This used to zero the y and z components of the quaternion
            // itself, which are not independent axis rotations: the result was a non-unit
            // quaternion and an arbitrary pitch that drifted as the camera orbited.
            Vector3 toCamera = _view.transform.position - _cameraTransform.position;
            toCamera.y = 0f;

            if (toCamera.sqrMagnitude < 1e-6f)
                return;

            _view.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
        }
    }
}