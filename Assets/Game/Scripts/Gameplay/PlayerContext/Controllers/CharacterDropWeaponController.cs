using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterDropWeaponController : IPlayerContextInit, IPlayerContextTick
    {
        private IEntity _character;
        private GameContext _gameContext;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter().Value;
            _gameContext = GameContext.Instance;
        }

        public void Tick(IPlayerContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                DropWeaponUseCase.DropWeapon(_character, _gameContext);
            }
        }
    }
}