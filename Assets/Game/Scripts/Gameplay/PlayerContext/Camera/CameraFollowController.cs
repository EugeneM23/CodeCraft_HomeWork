using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CameraFollowController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private IReactiveVariable<IEntity> _characterSource;
        private Subscription<IEntity> _characterSubscription;

        private Transform _cameraPoint;
        private IValue<Vector3> _offset;
        private Transform _cameraRoot;
        private IValue<int> _speed;

        public void Init(IPlayerContext context)
        {
            _offset = context.GetCameraOffset();
            _cameraRoot = context.GetCameraRoot();
            _speed = context.GetCameraSpeed();

            _characterSource = context.GetCharacter();
            _characterSubscription = _characterSource.Observe(OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context) => _characterSubscription.Dispose();

        private void OnCharacterChanged(IEntity character) => _cameraPoint = character?.GetCameraPoint();

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            if (_cameraPoint == null) return;

            CameraFollowUseCase.Follow(_cameraRoot, _cameraPoint, deltaTime, _speed.Value, _offset.Value);
        }
    }
}
