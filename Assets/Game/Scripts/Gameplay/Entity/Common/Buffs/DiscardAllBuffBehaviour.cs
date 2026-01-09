using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;

namespace Game
{
    public class DiscardAllBuffBehaviour : IEntityInit
    {
        private IEntity _character;
        private Health _health;

        public void Init(in IEntity entity)
        {
            _character = entity;
            _health = entity.GetHealth();

            _health.OnHealthChanged += DiscardAllBuffs;
        }

        private void DiscardAllBuffs(int _)
        {
            BuffUseCase.DiscardAll(_character);
        }
    }
}