using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    [CreateAssetMenu(fileName = "JumpAspect", menuName = "BuffSystem/Aspect/JumpAspect")]
    public class JumpAspect : ScriptableEntityAspect
    {
        [SerializeField] private float _jumpForce = 2;

        public override void Apply(IEntity entity)
        {
            entity.AddJumpableTag();
            entity.AddJumpForce(_jumpForce);
            entity.AddJumpEvent(new Atomic.Elements.Event());
            entity.AddJumpCondition(new InlineFunction<bool>(() => entity.GetHealth().GetCurrent() > 0));
        }

        public override void Discard(IEntity entity)
        {
            entity.DelJumpableTag();
            entity.DelJumpForce();
            entity.DelJumpEvent();
            entity.DelJumpCondition();
        }
    }
}
