using Atomic.Elements;
using Atomic.Entities;
using Atomic.Extensions;
using UnityEngine;

namespace Game.Gameplay
{
    [CreateAssetMenu(fileName = "MoveSpeedAspect", menuName = "Gameplay/MoveSpeedAspect")]
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