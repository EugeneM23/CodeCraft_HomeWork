using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "MoveSpeedBuffEffect", menuName = "Gameplay/MoveSpeedBuffEffect")]
    public class MoveSpeedBuff : BaseBuff
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