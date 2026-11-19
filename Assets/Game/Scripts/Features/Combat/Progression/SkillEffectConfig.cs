using UnityEngine;

namespace Game
{
    /// <summary>
    /// The numbers of one run skill per level 1..5 (GDD §3), keyed by the core's <see cref="SkillId"/>; the cards'
    /// texts and the group are the core's <see cref="SkillConfig"/>. What a number means depends on the skill (see
    /// <see cref="SkillEffectsUseCase"/>): turret — turrets, air strike — its level, ricochet — bounces, rate of fire —
    /// reload factor, armor — max health factor, magnet — pickup radius factor. Missing or short arrays fall back to
    /// the code defaults of <see cref="SkillEffectTable"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillEffectConfig", menuName = "Game/Combat/Skill Effect Config")]
    public sealed class SkillEffectConfig : ScriptableObject
    {
        [SerializeField] private SkillId _id;

        [Tooltip("The main number of each level 1..5 (turrets, bounces, reload factor, health factor, radius factor...)")]
        [SerializeField] private float[] _values = new float[0];

        [Tooltip("A second number per level where the skill has one (turret: damage factor); empty otherwise")]
        [SerializeField] private float[] _extra = new float[0];

        public SkillId Id => _id;

        public float[] Values => _values;

        public float[] Extra => _extra;

        /// <summary>Setup for tests and editor tools.</summary>
        public void Configure(SkillId id, float[] values, float[] extra = null)
        {
            _id = id;
            _values = values ?? new float[0];
            _extra = extra ?? new float[0];
        }
    }
}
