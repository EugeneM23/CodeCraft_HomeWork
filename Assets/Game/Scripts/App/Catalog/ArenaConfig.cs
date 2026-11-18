using UnityEngine;

namespace Game
{
    /// <summary>
    /// One arena of a chapter in order (GDD §10): what it holds. The arena team adds its generator settings here and AI
    /// its wave table, in their own fields; the save keeps only the id.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Run/Arena", fileName = "ArenaConfig")]
    public sealed class ArenaConfig : ScriptableObject
    {
        [SerializeField] private ArenaId _id;
        [SerializeField] private ArenaKind _kind = ArenaKind.Horde;
        [Tooltip("What the arena teaches or adds, for the designer (GDD §10)")]
        [SerializeField, TextArea] private string _note;

        public ArenaId Id => _id;
        public ArenaKind Kind => _kind;
        public string Note => _note;

        /// <summary>Setup for tests and the catalog builder.</summary>
        public void Configure(ArenaId id, ArenaKind kind)
        {
            _id = id;
            _kind = kind;
        }
    }
}
