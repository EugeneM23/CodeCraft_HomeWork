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

        public void Init(IGameContext context)
        {
            _character = context.GetCharacter();
            _offset = context.GetCameraOffset();
            _camera = context.GetCamera().transform;
        }

        public void  OnLateUpdate(IContext context, float deltaTime)
        {
            _camera.position = _character.GetTransform().position + _offset.Value;
        }
    }
}