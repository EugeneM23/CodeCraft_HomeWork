using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    [CreateAssetMenu(fileName = "JumpAspect", menuName = "BuffSystem/Aspect/JumpAspect")]
    public class JumpAspect : ScriptableEntityAspect
    {
        [SerializeField] private float _jumpForce = 4.85f;

        // The adds are guarded because an entity can already carry the jump values: JumpBuff grants
        // the same four, a composite that threw part-way through leaves some of them behind, and a
        // domain reload wipes the trigger's bookkeeping while the entity keeps its state. The old
        // AddValue was TryAdd and ignored all of that silently; the new one throws and takes the
        // rest of the composite with it, so the check restores the previous behaviour: first grant
        // wins.
        public override void Apply(IEntity entity)
        {
            entity.AddJumpableTag();

            if (!entity.HasJumpForce())
                entity.AddJumpForce(_jumpForce);

            if (!entity.HasJumpEvent())
                entity.AddJumpEvent(new Atomic.Elements.Event());

            if (!entity.HasJumpCondition())
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
