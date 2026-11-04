using Atomic.Entities;

namespace Game
{
    public abstract class TemporaryBuff : BuffBase, IEntityFixedTick
    {
        private readonly float _duration;
        private readonly string _name;
        private float _currentTime;

        protected TemporaryBuff(BuffConfig config) : base(config)
        {
            _duration = config.Duration;
            _name = config.Name;
        }

        public sealed override bool Apply(IEntity entity)
        {
            if (!IsValid(entity))
                return false;

            entity.AddBehaviour(this);
            OnApply(entity);
            return true;
        }

        public sealed override void Discard(IEntity entity)
        {
            // Deliberately not gated on IsValid. IsValid is a precondition for applying, not an
            // identity check, and the state it tests can lapse while the buff is running — drop
            // the weapon and ExtraDamageBuff.IsValid turns false. Gating here meant the expiry
            // removed the buff from the list but left the behaviour attached and the bonus applied,
            // so the weapon kept the extra damage and the halved cooldown for good.
            entity.DelBehaviour(this);
            OnDiscard(entity);
        }

        protected abstract bool IsValid(IEntity entity);
        protected abstract void OnApply(IEntity entity);
        protected abstract void OnDiscard(IEntity entity);

        public void FixedTick(IEntity entity, float deltaTime)
        {
            _currentTime += deltaTime;
 
            if (_currentTime >= _duration)
                BuffUseCase.Discard(entity, _name);
        }
    }
}