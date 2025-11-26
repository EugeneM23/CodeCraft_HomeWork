using Atomic.Contexts;
using Atomic.Entities;
using Modules.Common;
using UnityEngine;

namespace Game
{
    public class CharacterRotateController : IContextInit<IPlayerContext>, IContextLateUpdate
    {
        private Joystick _joystick;
        private IEntity _character;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter().Value;
            _joystick = context.GetRotateJoystick();
        }

        private void Rotate()
        {
            Vector3 direction = new Vector3(_joystick.Horizontal, 0, _joystick.Vertical);


            if (direction.magnitude > 1f)
                direction.Normalize();

            _character.GetRotateDirection().Value = direction;
        }

        public void OnLateUpdate(IContext context, float deltaTime) => Rotate();
    }
}