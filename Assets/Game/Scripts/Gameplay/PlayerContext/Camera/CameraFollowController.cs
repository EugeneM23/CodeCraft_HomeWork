using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CameraFollowController : IContextInit<IPlayerContext>, IContextLateUpdate
    {
        private IEntity _character;
        private IValue<Vector3> _offset;
        private Transform _cameraRoot;
        private IValue<int> _speed;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter();
            _offset = context.GetCameraOffset();
            _cameraRoot = context.GetCameraRoot();
            _speed = context.GetCameraSpeed();
        }

        public void OnLateUpdate(IContext context, float deltaTime)
        {
            CameraFollowUseCase.Follow(_cameraRoot, _character.GetTransform(), deltaTime, _speed.Value, _offset.Value);
        }
    }
}