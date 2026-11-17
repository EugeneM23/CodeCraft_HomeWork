using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class PlayerCharacterDropWeaponController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextTick
    {
        private IReactiveVariable<IEntity> _characterSource;
        private Subscription<IEntity> _characterSubscription;
        private IEntity _character;
        private GameContext _gameContext;

        public void Init(IPlayerContext context)
        {
            _characterSource = context.GetCharacter();
            _characterSubscription = _characterSource.Observe(OnCharacterChanged);
            _gameContext = GameContext.Instance;
        }

        public void Dispose(IPlayerContext context) => _characterSubscription.Dispose();

        private void OnCharacterChanged(IEntity character) => _character = character;

        public void Tick(IPlayerContext context, float deltaTime)
        {
            if (!Input.GetKeyDown(KeyCode.G)) return;

            if (_character != null)
                DropWeaponUseCase.DropWeapon(_character, _gameContext);
        }
    }
}
