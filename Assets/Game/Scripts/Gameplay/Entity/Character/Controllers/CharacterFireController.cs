using Atomic.Contexts;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterFireController : IContextInit<IPlayerContext>, IContextLateUpdate
    {
        private IEntity _character;

        public void OnLateUpdate(IContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (_character.TryGetWeapon(out var weapon) && weapon != null)
                    weapon.Value.GetFireAction().Invoke();
            }
        }

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter();
        }
    }
}