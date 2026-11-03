using Atomic.Entities;
using Game.Gameplay;
using UnityEngine;

namespace Game
{
    public class CharacterJumpController : IPlayerContextInit, IPlayerContextLateTick
    {
        private IEntity _character;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter().Value;
        }

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            if (!_character.HasJumpableTag()) return;

            if (!_character.GetJumpCondition().Invoke()) return;

            if (Input.GetKey(KeyCode.V))
                JumpUseCase.Jump(_character);
        }
    }
}