using Atomic.Entities;
using Game.Gameplay;

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

        // Shares the refcounted grant with JumpAspect: the effect area and this buff can be active
        // at the same time, and whichever ends first no longer takes jumping away from the other.
        protected override void OnApply(IEntity entity) => JumpUseCase.AddJumpSource(entity, _jumpForce);

        protected override void OnDiscard(IEntity entity) => JumpUseCase.DelJumpSource(entity);
    }
}
