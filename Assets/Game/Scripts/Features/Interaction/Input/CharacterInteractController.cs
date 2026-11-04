using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterInteractController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextTick
    {
        private IReactiveVariable<IEntity> _characterSource;
        private Subscription<IEntity> _characterSubscription;
        private IEntity _character;

        public void Init(IPlayerContext context)
        {
            _characterSource = context.GetCharacter();
            _characterSubscription = _characterSource.Observe(OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context) => _characterSubscription.Dispose();

        private void OnCharacterChanged(IEntity character) => _character = character;

        public void Tick(IPlayerContext context, float deltaTime)
        {
            if (!Input.GetKeyDown(KeyCode.E)) return;

            if (_character != null)
                InteractUseCase.InteractWithCharacter(_character);
        }
    }
}
