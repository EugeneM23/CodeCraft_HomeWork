using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterFireController : IContextInit<IPlayerContext>, IContextLateUpdate
    {
        private IReactiveVariable<IEntity> _weapon;

        public void Init(IPlayerContext context)
        {
            _weapon = context.GetCharacter().GetWeapon();
        }

        public void OnLateUpdate(IContext context, float deltaTime)
        {
            if (_weapon.Value == null || !_weapon.Value.GetFireCondition().Value) return;

            if (Input.GetKey(KeyCode.Space))
            {
                _weapon.Value.GetFireAction().Invoke();
            }
        }
    }
}