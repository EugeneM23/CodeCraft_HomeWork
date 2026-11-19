using UnityEngine;

namespace Game.Horde
{
    /// <summary>
    /// Baked art of a horde monster (written by <c>Tools/Horde/Bake Regax VAT</c> or <c>Bake Zombie VAT</c>): the low-poly
    /// crowd mesh with the vertex id in UV1, its vertex animation texture (positions, then normals,
    /// one row per frame), the corpse mesh in the death pose, the material and the clip rows.
    /// </summary>
    [CreateAssetMenu(fileName = "HordeArtConfig", menuName = "Game/Horde/Art Config")]
    public sealed class HordeArtConfig : ScriptableObject
    {
        [Tooltip("Crowd mesh: the decimated skin in bind pose, vertex id in UV1.x.")]
        public Mesh mesh;

        [Tooltip("Static mesh of the last frame of the death clip (same decimation).")]
        public Mesh corpseMesh;

        [Tooltip("Material on Game/HordeVAT with the VAT texture set.")]
        public Material material;

        [Tooltip("RGBAHalf: width = vertices, rows 0..frames-1 positions, rows frames..2*frames-1 normals.")]
        public Texture2D vat;

        public int vertexCount;

        [Tooltip("Animation rows in the VAT (the normal rows follow them).")]
        public int frameCount;

        public HordeClip run;
        public HordeClip attack;
        public HordeClip idle;

        [Tooltip("Bounds of every baked frame, model space (feet at y 0).")]
        public Bounds bounds;

        [Tooltip("Skin colours of the parts a torn body throws (VoxelSpray.Chunks: head, torso, arms, legs), sampled from the " +
                 "material's atlas over the mesh by Tools/Horde/Sample Torn Skins (the crowd mesh is not readable in play). Empty: plain greens.")]
        public HordeTornSkins tornSkins = new();

        [Tooltip("Triangles of the crowd mesh / of the source skin.")]
        public int triangles;

        public int sourceTriangles;

        [Tooltip("Mesh LOD level of the source the crowd mesh was cut from (0 = full mesh).")]
        public int meshLod;

        [Header("Size")]
        [Tooltip("Height of the model over the Regax's (2.6 m) at the same render scale: the simulation's mouth, chest, aim point, culling sphere and emerging depth follow it. Regax: 1.")]
        public float bodyScale = 1f;

        [Tooltip("Walking speed the run clip was authored for at render scale 1, m/s; 0 keeps the horde options' own (5).")]
        public float runClipSpeed;

        [Tooltip("Body radius (hull reach, crush box, separation) and bite reach over the kinds' own: a bigger model than the one the kinds were tuned on reaches further. Regax: 1.")]
        public float reachScale = 1f;

        [Header("Kinds")]
        [Tooltip("Tint per kind row (0 Crawler, 1 Spitter...); alpha 0 or a missing row keeps the material's colour. The scale comes from the kind table.")]
        public Color[] kindTints = { new(0f, 0f, 0f, 0f), new(0.32f, 0.3f, 0.04f, 1f) };

        [Tooltip("Emission per kind row (used with its tint).")]
        public Color[] kindEmission = { Color.black, new(0.08f, 0.12f, 0f, 1f) };

        [Header("Acid")]
        [Tooltip("Mesh of a shot in flight; empty: Unity's built-in sphere.")]
        public Mesh acidMesh;

        [Tooltip("Material of a shot in flight (Game/HordeVAT, no albedo texture); empty: the crowd's material.")]
        public Material acidMaterial;

        [Tooltip("Size of a shot, m.")]
        public float acidSize = 0.35f;

        public Color acidColor = new(0.35f, 0.9f, 0.05f, 1f);

        public Color acidEmission = new(0.25f, 0.6f, 0f, 1f);

        public Color TintOf(int kind) => kindTints != null && kind < kindTints.Length ? kindTints[kind] : default;

        public Color EmissionOf(int kind) => kindEmission != null && kind < kindEmission.Length ? kindEmission[kind] : Color.black;
    }
}

namespace Game.Horde
{
    /// <summary>Vertex colours (as the voxel palettes take them) of each part of a torn body, from the zombie's atlas.</summary>
    [System.Serializable]
    public sealed class HordeTornSkins
    {
        public UnityEngine.Color32[] head = System.Array.Empty<UnityEngine.Color32>();
        public UnityEngine.Color32[] torso = System.Array.Empty<UnityEngine.Color32>();
        public UnityEngine.Color32[] arms = System.Array.Empty<UnityEngine.Color32>();
        public UnityEngine.Color32[] legs = System.Array.Empty<UnityEngine.Color32>();

        public bool IsEmpty => head.Length == 0 && torso.Length == 0 && arms.Length == 0 && legs.Length == 0;

        /// <summary>In the order of VoxelSpray.Chunks' parts: head, torso, left and right arm, left and right leg. Null when empty.</summary>
        public UnityEngine.Color32[][] ForChunks() => IsEmpty ? null : new[] { head, torso, arms, arms, legs, legs };
    }
}
