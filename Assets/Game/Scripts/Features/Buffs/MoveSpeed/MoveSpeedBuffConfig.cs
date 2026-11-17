using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "MoveSpeedBuffConfig", menuName = "BuffSystem/Buff/MoveSpeedBuffConfig")]
    public class MoveSpeedBuffConfig : BuffConfig
    {
        [field: SerializeField] public float Speed { get; private set; }

        public override BuffBase CreateBuff() => new MoveSpeedBuff(this);
    }
}