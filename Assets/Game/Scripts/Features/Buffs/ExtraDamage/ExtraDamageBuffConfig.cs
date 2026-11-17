using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "ExtraDamageBuffConfig", menuName = "BuffSystem/Buff/ExtraDamageBuffConfig")]
    public class ExtraDamageBuffConfig : BuffConfig
    {
        [field: SerializeField] public int Damage { get; private set; }

        /// <summary>
        /// What the weapon's cooldown is multiplied by while the buff holds: 0.5 fires twice as fast,
        /// 1 leaves the rate alone (the damage zone only adds damage).
        /// </summary>
        [field: SerializeField, Min(0.01f)] public float CooldownFactor { get; private set; } = 0.5f;

        public override BuffBase CreateBuff()
        {
            return new ExtraDamageBuff(this);
        }
    }
}
