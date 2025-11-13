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
                _character.GetWeapon().GetFireAction().Invoke();
        }

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter();
        }
    }
}