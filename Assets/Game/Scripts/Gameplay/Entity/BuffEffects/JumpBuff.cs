using Atomic.Elements;
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

        // Same guards as JumpAspect, and for the same reason: the effect-area aspect grants these
        // four as well, so whichever gets there first wins instead of the second one throwing.
        protected override void OnApply(IEntity entity)
        {
            entity.AddJumpableTag();

            if (!entity.HasJumpForce())
                entity.AddJumpForce(_jumpForce);

            if (!entity.HasJumpEvent())
                entity.AddJumpEvent(new Atomic.Elements.Event());

            if (!entity.HasJumpCondition())
                entity.AddJumpCondition(new InlineFunction<bool>(() => entity.GetHealth().GetCurrent() > 0));
        }

        protected override void OnDiscard(IEntity entity)
        {
            entity.DelJumpableTag(); 
            entity.DelJumpForce();
            entity.DelJumpEvent();
            entity.DelJumpCondition();
        }
    }
}