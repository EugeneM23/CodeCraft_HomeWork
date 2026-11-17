using Atomic.Entities;

namespace Game
{
    public class JumpBuff : TemporaryBuff
    {
        private readonly float _jumpForce;

        public JumpBuff(JumpBuffConfig config) : base(config)
        {
            _jumpForce = config.JumpForce;
        }

        protected override bool IsValid(IEntity entity)
        {
            return true;
        }

        // The grant is refcounted: the jump zone's buff (ZoneJump) and a pickup's can be active at
        // the same time, and whichever ends first no longer takes jumping away from the other.
        protected override void OnApply(IEntity entity) => JumpUseCase.AddJumpSource(entity, _jumpForce);

        protected override void OnDiscard(IEntity entity) => JumpUseCase.DelJumpSource(entity);
    }
}
