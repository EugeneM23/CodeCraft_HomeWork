using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    [CreateAssetMenu(fileName = "MoveSpeedAspect", menuName = "BuffSystem/Aspect/MoveSpeedAspect")]
    public class MoveSpeedAspect : ScriptableEntityAspect
    {
        // Had no initializer, so a freshly created asset multiplied move speed by zero on enter
        // and divided by zero on exit: the character froze and then every position update produced
        // NaN, corrupting the transform for good.
        [SerializeField] private float _multiplier = 1f;

        public override void Apply(IEntity entity)
        {
            if (_multiplier <= 0f) return;

            if (entity.TryGetMoveSpeed(out var moveSpeed)) moveSpeed.Value *= _multiplier;
        }

        public override void Discard(IEntity entity)
        {
            if (_multiplier <= 0f) return;

            if (entity.TryGetMoveSpeed(out var moveSpeed)) moveSpeed.Value /= _multiplier;
        }
    }
}
