using Atomic.Contexts;
using Atomic.Entities;
using Modules.Common;
using UnityEngine;

namespace Game
{
    public class CharacterMoveController : IContextInit<IPlayerContext>, IContextLateUpdate
    {
        private Joystick _joystick;
        private IEntity _character;

        private void Move()
        {
            Vector3 direction = new Vector3(_joystick.Horizontal, 0, _joystick.Vertical);

            _character.GetMoveDirection().Value = direction;
        }

        public void OnLateUpdate(IContext context, float deltaTime) => Move();

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter();
            _joystick = context.GetMoveJoystick();
        }
    }
}