using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Samples movement input and publishes it as a direction on the character.
    /// The on-screen joysticks were removed: this is keyboard only now.
    /// </summary>
    public class CharacterMoveController : IPlayerContextInit, IPlayerContextLateTick
    {
        private IEntity _character;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter().Value;
        }

        private void Move()
        {
            Vector3 direction = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));

            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            _character.GetMoveDirection().Value = direction;
        }

        public void LateTick(IPlayerContext context, float deltaTime) => Move();
    }
}
