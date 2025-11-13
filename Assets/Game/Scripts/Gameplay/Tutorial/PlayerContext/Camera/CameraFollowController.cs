using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using Game.Scripts.Gameplay.Tutorial.PlayerContext;
using UnityEngine;

namespace Game
{
    public class CameraFollowController : IContextInit<IPlayerContext>, IContextLateUpdate
    {
        private IEntity _character;
        private IValue<Vector3> _offset;
        private Transform _camera;
        private IValue<int> _speed;


        public void OnLateUpdate(IContext context, float deltaTime)
        {
            FollowObjectUseCase.Follow(_camera, _character.GetTransform(), deltaTime, _speed.Value, _offset.Value);
        }

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter();
            _offset = context.GetCameraOffset();
            _camera = context.GetCamera().transform;
            _speed = context.GetCameraSpeed();

        }
    }
}