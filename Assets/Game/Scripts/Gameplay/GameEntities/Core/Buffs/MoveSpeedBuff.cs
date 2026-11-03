using Atomic.Entities;

namespace Game
{
    public class MoveSpeedBuff : TemporaryBuff
    {
        private readonly float _multiplier;

        public MoveSpeedBuff(MoveSpeedBuffConfig config) : base(config)
        {
            _multiplier = config.Speed;
        }

        protected override bool IsValid(IEntity entity) => entity.HasMoveSpeed();

        protected override void OnApply(IEntity entity)
        {
            entity.GetMoveSpeed().Value *= _multiplier;
        }

        protected override void OnDiscard(IEntity entity)
        {
            entity.GetMoveSpeed().Value /= _multiplier;
        }
    }
}