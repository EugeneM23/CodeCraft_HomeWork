using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "JumpBuffConfig", menuName = "Gameplay/JumpBuffConfig")]
    public class JumpBuffConfig : BuffConfig
    {
        [field: SerializeField] public float JumpForce { get; private set; }

        public override BuffBase CreateBuff()
        {
            return new JumpBuff(this);
        }
    }

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

        protected override void OnApply(IEntity entity)
        {
            entity.AddJumpableTag();
            entity.AddJumpForce(_jumpForce);
            entity.AddJumpEvent(new BaseEvent());
            entity.AddJumpCondition(new BaseFunction<bool>(() => entity.GetHealth().GetCurrent() > 0));
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