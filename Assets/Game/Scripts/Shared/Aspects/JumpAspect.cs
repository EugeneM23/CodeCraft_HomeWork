using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "JumpAspect", menuName = "BuffSystem/Aspect/JumpAspect")]
    public class JumpAspect : ScriptableEntityAspect
    {
        [SerializeField] private float _jumpForce = 4.85f;

        // The grant is refcounted on the entity, so overlapping sources — two effect areas, or an
        // area plus an active JumpBuff — no longer cancel each other out. See JumpUseCase.
        public override void Apply(IEntity entity) => JumpUseCase.AddJumpSource(entity, _jumpForce);

        public override void Discard(IEntity entity) => JumpUseCase.DelJumpSource(entity);
    }
}
