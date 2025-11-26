using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CameraShakeBehaviour : IEntityUpdate, IEntityInit
    {
        private Transform _cameraTransform;
        private BaseEvent<CameraShakeArgs> _cameraShakeEvent;

        private bool _isShaking;
        private float _shakeTimeRemaining;
        private float _shakeStrength;
        private float _shakeDuration;
        private Vector3 _originalPosition;

        public void Init(in IEntity entity)
        {
            _cameraShakeEvent = entity.GetCameraShakeEvent();
            _cameraShakeEvent.Subscribe(OnShake);
            _cameraTransform = entity.GetTransform();
        }

        private void OnShake(CameraShakeArgs args)
        {
            _isShaking = true;
            _shakeDuration = args.ShakeDuration;
            _shakeStrength = args.ShakeStrength;
            _shakeTimeRemaining = _shakeDuration;
            _originalPosition = _cameraTransform.localPosition;
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (!_isShaking) return;

            if (_shakeTimeRemaining > 0)
            {
                ShakeTransformUseCase.ShakePosition(
                    _cameraTransform,
                    _originalPosition,
                    _shakeStrength,
                    _shakeTimeRemaining / _shakeDuration
                );

                _shakeTimeRemaining -= deltaTime;
            }
            else
            {
                _cameraTransform.localPosition = _originalPosition;
                _isShaking = false;
            }
        }
    }
}