using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterFireController : IContextInit<IPlayerContext>, IContextLateUpdate
    {
        private IEntity _character;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter();
        }

        public void OnLateUpdate(IContext context, float deltaTime)
        {
            if (!_character.GetFireCondition().Value) return;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _character.GetWeapon().Value.GetFireAction().Invoke();
            }
        }
    }
}