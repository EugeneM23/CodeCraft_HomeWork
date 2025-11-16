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

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter();
            _joystick = context.GetMoveJoystick();
        }

        private void Move()
        {
            Vector3 direction = new Vector3(_joystick.Horizontal, 0, _joystick.Vertical);

            if (direction == Vector3.zero)
                direction = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

            if (direction.magnitude > 1f)
                direction.Normalize();

            _character.GetMoveDirection().Value = direction;
        }

        public void OnLateUpdate(IContext context, float deltaTime) => Move();
    }
}