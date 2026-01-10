using Atomic.Entities;
using Game.Gameplay;

namespace Game
{
    public abstract class TemporaryBuff : BuffBase, IEntityFixedUpdate
    {
        private readonly float _duration;
        private readonly string _name;
        private float _currentTime;

        protected TemporaryBuff(BuffConfig config) : base(config)
        {
            _duration = config.Duration;
            _name = config.Name;
        }

        public sealed override void Apply(IEntity entity)
        {
            if (IsValid(entity))
            {
                entity.AddBehaviour(this);
                OnApply(entity);
            }
        }

        public sealed override void Discard(IEntity entity)
        {
            if (IsValid(entity))
            {
                entity.DelBehaviour(this);
                OnDiscard(entity);
            }
        }

        protected abstract bool IsValid(IEntity entity);
        protected abstract void OnApply(IEntity entity);
        protected abstract void OnDiscard(IEntity entity);

        public void OnFixedUpdate(in IEntity entity, in float deltaTime)
        {
            _currentTime += deltaTime;
 
            if (_currentTime >= _duration)
                BuffUseCase.Discard(entity, _name);
        }
    }
}