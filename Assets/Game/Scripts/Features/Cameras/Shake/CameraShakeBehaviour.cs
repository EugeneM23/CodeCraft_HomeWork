using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Displaces the camera transform while a shake is running.
    ///
    /// The rest pose is captured once, in Init. It used to be re-read on every shake, including
    /// shakes that started while the transform was already displaced — so automatic fire walked
    /// the camera away from its mount, one offset at a time, and never came back.
    /// </summary>
    public class CameraShakeBehaviour : IEntityInit, IEntityTick, IEntityDispose
    {
        private Transform _cameraTransform;
        private IEvent<CameraShakeArgs> _cameraShakeEvent;

        private bool _isShaking;
        private float _shakeTimeRemaining;
        private float _shakeStrength;
        private float _shakeDuration;
        private Vector3 _restPosition;
        private bool _restCaptured;

        public void Init(IEntity entity)
        {
            _cameraShakeEvent = entity.GetCameraShakeEvent();
            _cameraShakeEvent.Subscribe(OnShake);
            _cameraTransform = entity.GetTransform();

            _restPosition = _cameraTransform.localPosition;
            _restCaptured = true;
            _isShaking = false;
        }

        public void Dispose(IEntity entity)
        {
            // Without this the handler stayed on the camera entity event for the rest of the
            // process, holding a destroyed transform, and a reload added a second copy.
            _cameraShakeEvent.Unsubscribe(OnShake);
            _cameraShakeEvent = null;

            if (_restCaptured && _isShaking && _cameraTransform != null)
                _cameraTransform.localPosition = _restPosition;

            _isShaking = false;
        }

        private void OnShake(CameraShakeArgs args)
        {
            // Deliberately does not touch _restPosition.
            _shakeDuration = Mathf.Max(args.ShakeDuration, 0.0001f);
            _shakeStrength = args.ShakeStrength;
            _shakeTimeRemaining = _shakeDuration;
            _isShaking = true;
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (!_isShaking) return;

            _shakeTimeRemaining -= deltaTime;

            if (_shakeTimeRemaining <= 0f)
            {
                _cameraTransform.localPosition = _restPosition;
                _isShaking = false;
                return;
            }

            ShakeTransformUseCase.ShakePosition(
                _cameraTransform,
                _restPosition,
                _shakeStrength,
                _shakeTimeRemaining / _shakeDuration
            );
        }
    }
}
