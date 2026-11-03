using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterFireController : IPlayerContextInit, IPlayerContextLateTick
    {
        private IEntity _character;
        private IReactiveVariable<IEntity> _weapon;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter().Value;
            _weapon = _character.GetWeapon();
        }

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            if (!_character.GetFireCondition().Invoke()) return;

            if (Input.GetKey(KeyCode.Space))
                _character.GetFireAction().Invoke();
        }
    }
}