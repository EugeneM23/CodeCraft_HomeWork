using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    [CreateAssetMenu(fileName = "MoveSpeedAspect", menuName = "BuffSystem/Aspect/MoveSpeedAspect")]
    public class MoveSpeedAspect : ScriptableEntityAspect
    {
        [SerializeField] private float _multiplier;

        public override void Apply(IEntity entity)
        {
            if (entity.TryGetMoveSpeed(out var moveSpeed)) moveSpeed.Value *= _multiplier;
        }

        public override void Discard(IEntity entity)
        {
            if (entity.TryGetMoveSpeed(out var moveSpeed)) moveSpeed.Value /= _multiplier;
        }
    }
}
