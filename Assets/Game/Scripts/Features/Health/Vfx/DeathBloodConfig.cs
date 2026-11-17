using UnityEngine;

namespace Game
{
    /// <summary>
    /// Which KriptoFX volumetric blood effects a death can throw, and how they are sized and
    /// coloured for Regax. Read by <see cref="DeathBloodBehaviour"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Death Blood Config", fileName = "DeathBloodConfig")]
    public sealed class DeathBloodConfig : ScriptableObject
    {
        [Tooltip("Pack effects (Blood1..15 and the like). A death picks one of them.")]
        public GameObject[] variants;

        [Tooltip("Scale of the whole effect. The pack is sized for a person.")]
        public float scale = 2.72f;

        [Tooltip("On top of the scale: the volumetric gush (the BFX_Blood meshes).")]
        public float fluidScale = 2f;

        [Tooltip("On top of the scale: the pools on the floor (the BFX_Decal boxes).")]
        public float decalScale = 0.8f;

        [Tooltip("Height above the anchor the effect starts at; the pool finds the floor itself.")]
        public float height = 0.3f;

        [Tooltip("Replaces the pack's red on the gush (BFX_Blood _Color).")]
        public Color fluidColor = new(0.25f, 0.75f, 0.05f, 1f);

        [Tooltip("Replaces the pack's red on the pool (BFX_Decal _TintColor).")]
        public Color decalColor = new(0.13f, 0.44f, 0.02f, 1f);

        [Tooltip("Seconds before the effect is removed. The pack's pools last 30 s.")]
        public float lifetime = 35f;
    }
}
