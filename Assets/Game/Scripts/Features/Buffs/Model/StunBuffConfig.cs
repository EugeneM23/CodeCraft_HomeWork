using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "StunBuffConfig", menuName = "BuffSystem/Buff/StunBuffConfig")]
    public class StunBuffConfig : BuffConfig
    {
        [field: SerializeField] public float Time { get; private set; }

        public override BuffBase CreateBuff() => new StunBuff(this);
    }
}