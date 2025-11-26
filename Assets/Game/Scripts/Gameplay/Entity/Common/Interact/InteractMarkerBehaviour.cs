using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class InteractMarkerBehaviour : IEntityInit, IEntityDispose, IEntityLateUpdate
    {
        private IReactiveVariable<IEntity> _targetInteractable;
        private GameObject _view;
        private Vector3 _positionOffset;
        private readonly GameContext _gameContext;
        private IEntity _camera;

        public InteractMarkerBehaviour(GameObject view, GameContext gameContext)
        {
            _gameContext = gameContext;
            _view = view;
        }

        public void Init(in IEntity entity)
        {
            _targetInteractable = _gameContext.GetPlayerContext().GetCharacter().Value.GetTargetInteractable();
            _camera = _gameContext.GetPlayerContext().GetCamera();
            _targetInteractable.Subscribe(OnTargetChanged);
            _positionOffset = entity.GetPositionOffset();
        }

        public void Dispose(in IEntity entity)
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

        public void OnLateUpdate(in IEntity entity, in float deltaTime)
        {
            var transformRotation = Quaternion.LookRotation(_view.transform.position - _camera.GetTransform().position);
            transformRotation.z = 0;
            transformRotation.y = 0;
            _view.transform.rotation =
                transformRotation;
        }
    }
}