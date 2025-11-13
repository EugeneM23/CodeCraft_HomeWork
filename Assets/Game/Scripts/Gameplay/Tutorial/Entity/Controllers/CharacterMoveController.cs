using Atomic.Contexts;
using Atomic.Entities;
using Modules.Common;
using UnityEngine;

namespace Game
{
    public class CharacterMoveController : IContextInit<IGameContext>, IContextLateUpdate
    {
        private Joystick _joystick;
        private IEntity _character;

        private void Move()
        {
            Vector3 direction = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

            if (direction == Vector3.zero)
                direction = new Vector3(_joystick.Horizontal, 0, _joystick.Vertical);

            _character.GetMoveDirection().Value = direction;
        }

        public void Init(IGameContext context)
        {
            _character = context.GetCharacter();
            _joystick = context.GetMoveJoystick();
        }

        public void OnLateUpdate(IContext context, float deltaTime) => Move();
    }
}