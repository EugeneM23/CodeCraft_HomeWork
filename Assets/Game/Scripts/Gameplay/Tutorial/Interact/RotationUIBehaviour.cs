using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class RotationUIBehaviour : IEntityInit, IEntityDispose, IEntityUpdate
    {
        private IReactiveVariable<bool> _isInteracting;
        private Transform _uITransform;
        private bool _state;

        public void Init(in IEntity entity)
        {
            _uITransform = entity.GetUITransform();
            _isInteracting = entity.GetIsInteract();
            _isInteracting.Subscribe(OnInteract);
        }

        public void Dispose(in IEntity entity)
        {
            _isInteracting.Unsubscribe(OnInteract);
        }

        private void OnInteract(bool b)
        {
            Debug.Log(b);

            _state = b;
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (_state)
            {
                Debug.Log(Camera.main == null);
                Vector3 directionToCamera = Camera.main.transform.position - _uITransform.position;
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera, Vector3.up);
                RotateUseCase.Rotate(_uITransform, targetRotation, 100, deltaTime);
            }
        }
    }
}