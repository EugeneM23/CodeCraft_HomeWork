using Atomic.Contexts;
using Atomic.Entities;
using Game.Gameplay;
using UnityEngine;

namespace Game
{
    public class CharacterJumpController : IContextInit<IPlayerContext>, IContextLateUpdate
    {
        private IEntity _character;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter().Value;
        }

        public void OnLateUpdate(IContext context, float deltaTime)
        {
            if (!_character.HasJumpableTag()) return;

            if (!_character.GetJumpCondition().Invoke()) return;

            if (Input.GetKey(KeyCode.V))
                JumpUseCase.Jump(_character);
        }
    }
}