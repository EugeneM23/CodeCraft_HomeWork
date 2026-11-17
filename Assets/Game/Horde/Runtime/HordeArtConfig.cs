using UnityEngine;

namespace Game.Horde
{
    /// <summary>
    /// Baked art of a horde monster (written by <c>Tools/Horde/Bake Regax VAT</c>): the low-poly
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

        [Tooltip("Triangles of the crowd mesh / of the source skin.")]
        public int triangles;

        public int sourceTriangles;

        [Tooltip("Mesh LOD level of the source the crowd mesh was cut from (0 = full mesh).")]
        public int meshLod;
    }
}
