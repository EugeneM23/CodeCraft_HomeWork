using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "StunBuffConfig", menuName = "BuffSystem/Buff/StunBuffConfig")]
    public class StunBuffConfig : BuffConfig
    {
        public override BuffBase CreateBuff() => new StunBuff(this);
    }
}