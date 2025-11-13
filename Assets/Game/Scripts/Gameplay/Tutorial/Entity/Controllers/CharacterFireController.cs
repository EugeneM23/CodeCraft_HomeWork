using Atomic.Contexts;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterFireController : IContextInit<IGameContext>, IContextLateUpdate
    {
        private IEntity _character;

        public void Init(IGameContext context)
        {
            _character = context.GetCharacter();
        }

        public void OnLateUpdate(IContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.E))
                _character.GetWeapon().GetFireAction().Invoke();
        }
    }
}