using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "BlindBuffConfig", menuName = "BuffSystem/Buff/BlindBuffConfig")]
    public class BlindBuffConfig : BuffConfig
    {
        [field: Tooltip("Share of its move speed a blinded enemy stumbles about at.")]
        [field: SerializeField, Range(0f, 1f)] public float StumbleSpeed { get; private set; } = 0.35f;

        public override BuffBase CreateBuff() => new BlindBuff(this);
    }
}
