using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CameraFollowController : IContextInit<IGameContext>, IContextLateUpdate
    {
        private IEntity _character;
        private IValue<Vector3> _offset;
        private Transform _camera;
        private IValue<int> _speed;

        public void Init(IGameContext context)
        {
            _character = context.GetCharacter();
            _offset = context.GetCameraOffset();
            _camera = context.GetCamera().transform;
            _speed = context.GetCameraSpeed();
        }

        public void OnLateUpdate(IContext context, float deltaTime)
        {
            FollowObjectUseCase.Follow(_camera, _character.GetTransform(), deltaTime, _speed.Value, _offset.Value);
        }
    }
}