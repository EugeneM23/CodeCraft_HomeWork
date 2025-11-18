using Atomic.Contexts;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterDropWeaponController : IContextInit<IPlayerContext>, IContextUpdate
    {
        private IEntity _character;
        private GameContext _gameContext;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter();
            _gameContext = GameContext.Instance;
        }

        public void OnUpdate(IContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                DropWeaponUseCase.DropWeapon(_character, _gameContext);
            }
        }
    }
}