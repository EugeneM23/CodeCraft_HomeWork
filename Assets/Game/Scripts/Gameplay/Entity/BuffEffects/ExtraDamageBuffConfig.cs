using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "ExtraDamageBuffConfig", menuName = "Gameplay/ExtraDamageBuffConfig")]

    public class ExtraDamageBuffConfig : BuffConfig
    {
        [field: SerializeField] public int Damage { get; private set; }

        public override BuffBase CreateBuff()
        {
            return new ExtraDamageBuff(this);
        }
    }
}