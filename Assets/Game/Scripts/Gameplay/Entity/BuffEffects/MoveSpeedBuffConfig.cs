using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "MoveSpeedBuffConfig", menuName = "BuffSystem/Buff/MoveSpeedBuffConfig")]
    public class MoveSpeedBuffConfig : BuffConfig
    {
        [field: SerializeField] public int Speed { get; private set; }

        public override BuffBase CreateBuff() => new MoveSpeedBuff(this);
    }
}