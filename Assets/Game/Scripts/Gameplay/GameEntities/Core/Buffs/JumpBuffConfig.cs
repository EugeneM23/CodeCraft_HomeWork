using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "JumpBuffConfig", menuName = "BuffSystem/Buff/JumpBuffConfig")]
    public class JumpBuffConfig : BuffConfig
    {
        [field: SerializeField] public float JumpForce { get; private set; }

        public override BuffBase CreateBuff()
        {
            return new JumpBuff(this);
        }
    }
}