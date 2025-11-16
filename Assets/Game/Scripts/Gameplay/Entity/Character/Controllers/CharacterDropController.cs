using Atomic.Contexts;
using Atomic.Entities;
using TMPro;
using UnityEngine;

namespace Game
{
    public class CharacterDropWeaponController : IContextInit<IPlayerContext>, IContextUpdate
    {
        private IEntity _character;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter();
        }

        public void OnUpdate(IContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                CurrentWeaponUseCase.DropWeapon(_character);
            }
        }
    }
}