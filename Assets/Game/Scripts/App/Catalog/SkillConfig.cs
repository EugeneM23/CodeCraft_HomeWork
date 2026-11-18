using UnityEngine;

namespace Game
{
    /// <summary>
    /// A run skill (GDD §3): what the level-up cards show. The combat team keeps the numbers of its levels in its own
    /// fields or in its own config keyed by the same id.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Run/Skill", fileName = "SkillConfig")]
    public sealed class SkillConfig : ScriptableObject
    {
        public const int MAX_LEVEL = 5;

        [SerializeField] private SkillId _id;
        [SerializeField] private SkillGroup _group;
        [SerializeField] private string _displayName;
        [Tooltip("What level 1 gives")]
        [SerializeField, TextArea] private string _description;
        [Tooltip("What every next level adds")]
        [SerializeField, TextArea] private string _perLevel;
        [SerializeField] private Sprite _icon;
        [Tooltip("Offered in runs; off: written but not in the game yet (milestone 1 has 6 of 20)")]
        [SerializeField] private bool _inPool = true;

        public SkillId Id => _id;
        public SkillGroup Group => _group;
        public string DisplayName => _displayName;
        public string Description => _description;
        public string PerLevel => _perLevel;
        public Sprite Icon => _icon;
        public bool InPool => _inPool;

        /// <summary>Setup for tests and the catalog builder.</summary>
        public void Configure(SkillId id, SkillGroup group, bool inPool = true, string displayName = null)
        {
            _id = id;
            _group = group;
            _inPool = inPool;
            _displayName = displayName ?? id.Value;
        }
    }
}
