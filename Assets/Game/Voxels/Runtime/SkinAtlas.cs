using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxels
{
    /// <summary>
    /// A body's texture (the zombie's atlas) as voxel colours: read once (a GPU read-back when the texture is not
    /// readable) at <see cref="SIZE"/> texels a side and cut down to the <see cref="SLOTS"/> colours the mesh actually
    /// shows (a few rounds of k-means over the texels its vertices and faces sample). Each texel then names its nearest
    /// of them (<see cref="Index"/>), so a job can colour a voxel by the UV it was cut at (SkinnedVoxelizer), and a body
    /// part torn off as plain cubes can take the colours of its region of the mesh (<see cref="PartColours"/>). One per
    /// texture and mesh, kept for the run.
    /// </summary>
    public sealed class SkinAtlas : IDisposable
    {
        /// <summary>Colours a skin is cut down to: palette slots FleshPalette.FIRST_SKIN onward.</summary>
        public const int SLOTS = 24;

        /// <summary>Texels a side the texture is read at.</summary>
        public const int SIZE = 128;

        private const int ROUNDS = 8;

        private static readonly Dictionary<(int texture, int mesh), SkinAtlas> s_cache = new();

        /// <summary>Per texel (row-major, v up), the index of its colour in <see cref="Colours"/>.</summary>
        public NativeArray<byte> Index;

        /// <summary>The colours as the texture has them (sRGB), <see cref="SLOTS"/> or fewer.</summary>
        public readonly Color[] Colours;

        private readonly Vector3[] _vertices;
        private readonly Vector2[] _uvs;

        private SkinAtlas(Color[] colours, byte[] index, Vector3[] vertices, Vector2[] uvs)
        {
            Colours = colours;
            Index = new NativeArray<byte>(index, Allocator.Persistent);
            _vertices = vertices;
            _uvs = uvs;
        }

        public void Dispose()
        {
            if (Index.IsCreated)
                Index.Dispose();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            foreach (SkinAtlas atlas in s_cache.Values)
                atlas.Dispose();
            s_cache.Clear();
        }

        /// <summary>The texture a renderer's material shows: _BaseMap (URP), else its main texture. Null when none.</summary>
        public static Texture TextureOf(Material material)
        {
            if (material == null)
                return null;
            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
                return material.GetTexture("_BaseMap");
            return material.HasProperty("_MainTex") ? material.mainTexture : null;
        }

        /// <summary>
        /// The atlas of a texture over a mesh, made the first time (a few ms) and kept. Null when either is missing, the
        /// mesh has no UVs or cannot be read.
        /// </summary>
        public static SkinAtlas Of(Texture texture, Mesh mesh)
        {
            if (texture == null || mesh == null)
                return null;
            var key = (texture.GetInstanceID(), mesh.GetInstanceID());
            if (s_cache.TryGetValue(key, out SkinAtlas cached))
                return cached;

            SkinAtlas atlas = Make(texture, mesh);
            s_cache[key] = atlas;
            return atlas;
        }

        /// <summary>The index (into <see cref="Colours"/>) of the texel under a UV, wrapped.</summary>
        public static int IndexAt(NativeArray<byte> index, float2 uv)
        {
            uv -= math.floor(uv);
            int x = math.min(SIZE - 1, (int)(uv.x * SIZE));
            int y = math.min(SIZE - 1, (int)(uv.y * SIZE));
            return index[y * SIZE + x];
        }

        /// <summary>The atlas colour (sRGB) under a UV.</summary>
        public Color ColourAt(Vector2 uv) => Colours[IndexAt(Index, uv)];

        /// <summary>
        /// The colours of a region of the mesh as it stands (its bind pose, feet at its bounds' bottom): the vertices whose
        /// height is within [<paramref name="fromHeight"/>, <paramref name="toHeight"/>] (shares of the mesh's height) and
        /// whose distance sideways from its middle is within [<paramref name="fromSide"/>, <paramref name="toSide"/>]
        /// (shares of its half width), each as often as the region shows it, up to <paramref name="max"/>. Empty when no
        /// vertex is in the region.
        /// </summary>
        public Color32[] PartColours(float fromHeight, float toHeight, float fromSide, float toSide, int max = 12)
        {
            if (_vertices == null || _vertices.Length == 0)
                return Array.Empty<Color32>();
            Vector3 min = _vertices[0], maxV = _vertices[0];
            for (int i = 1; i < _vertices.Length; i++)
            {
                min = Vector3.Min(min, _vertices[i]);
                maxV = Vector3.Max(maxV, _vertices[i]);
            }

            float height = Mathf.Max(1e-4f, maxV.y - min.y);
            float middle = (min.x + maxV.x) * 0.5f;
            float half = Mathf.Max(1e-4f, (maxV.x - min.x) * 0.5f);
            var picked = new List<Color32>(max);
            int step = Mathf.Max(1, _vertices.Length / 2000);
            for (int i = 0; i < _vertices.Length && picked.Count < 4 * max; i += step)
            {
                float h = (_vertices[i].y - min.y) / height;
                float s = Mathf.Abs(_vertices[i].x - middle) / half;
                if (h < fromHeight || h > toHeight || s < fromSide || s > toSide)
                    continue;
                // As a vertex colour (linear in a linear project), as the voxel palettes and the debris take it.
                picked.Add(MeshVoxelizer.ToVertexColor(ColourAt(_uvs[i])));
            }

            if (picked.Count <= max)
                return picked.ToArray();
            var spread = new Color32[max];
            for (int i = 0; i < max; i++)
                spread[i] = picked[i * picked.Count / max];
            return spread;
        }

        /// <summary>
        /// The skin of each body part VoxelSpray.Chunks throws (head, torso, left and right arm, left and right leg), from the
        /// regions of the mesh standing: the head above 84 % of the height, the torso between the legs and the head near the
        /// middle, the arms out to the sides, the legs below 48 %. Made once and kept.
        /// </summary>
        public Color32[][] ChunkSkins()
        {
            if (_chunkSkins != null)
                return _chunkSkins;
            Color32[] head = PartColours(0.84f, 1f, 0f, 0.4f);
            Color32[] torso = PartColours(0.48f, 0.84f, 0f, 0.4f);
            Color32[] arms = PartColours(0.4f, 0.9f, 0.4f, 1f);
            Color32[] legs = PartColours(0f, 0.48f, 0f, 0.6f);
            _chunkSkins = new[] { head, torso, arms, arms, legs, legs };
            return _chunkSkins;
        }

        private Color32[][] _chunkSkins;

        private static SkinAtlas Make(Texture texture, Mesh mesh)
        {
            Color32[] texels = Read(texture);
            if (texels == null)
                return null;

            Vector3[] vertices = null;
            Vector2[] uvs = null;
            if (mesh.isReadable)
            {
                vertices = mesh.vertices;
                uvs = mesh.uv;
            }
            else if (Application.isPlaying)
            {
                // A mesh that is not readable cannot be read in play (the editor out of play can): no atlas, the plain skin.
                return null;
            }
            else
            {
                using Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(mesh);
                Mesh.MeshData m = data[0];
                if (m.HasVertexAttribute(VertexAttribute.TexCoord0))
                {
                    var v = new NativeArray<Vector3>(m.vertexCount, Allocator.Temp);
                    var u = new NativeArray<Vector2>(m.vertexCount, Allocator.Temp);
                    m.GetVertices(v);
                    m.GetUVs(0, u);
                    vertices = v.ToArray();
                    uvs = u.ToArray();
                    v.Dispose();
                    u.Dispose();
                }
            }

            if (uvs == null || uvs.Length == 0 || vertices == null || uvs.Length != vertices.Length)
                return null;

            // What the mesh shows: the texels under its vertices (a low-poly mesh maps whole faces to a flat patch).
            var samples = new List<Color>(uvs.Length);
            foreach (Vector2 uv in uvs)
                samples.Add(texels[TexelOf(uv)]);
            Color[] colours = KMeans(samples, SLOTS);

            var index = new byte[SIZE * SIZE];
            for (int t = 0; t < index.Length; t++)
                index[t] = (byte)Nearest(colours, texels[t]);
            return new SkinAtlas(colours, index, vertices, uvs);
        }

        private static int TexelOf(Vector2 uv)
        {
            float u = uv.x - Mathf.Floor(uv.x), v = uv.y - Mathf.Floor(uv.y);
            return Mathf.Min(SIZE - 1, (int)(v * SIZE)) * SIZE + Mathf.Min(SIZE - 1, (int)(u * SIZE));
        }

        /// <summary>The texture at SIZE x SIZE (sRGB values), through the GPU: works for a texture that is not readable.</summary>
        private static Color32[] Read(Texture texture)
        {
            RenderTexture target = RenderTexture.GetTemporary(SIZE, SIZE, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            var read = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false, false);
            read.ReadPixels(new Rect(0, 0, SIZE, SIZE), 0, 0, false);
            read.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Color32[] texels = read.GetPixels32();
            UnityEngine.Object.Destroy(read);
            return texels;
        }

        private static Color[] KMeans(List<Color> samples, int k)
        {
            k = Mathf.Min(k, samples.Count);
            var centres = new Color[k];
            for (int i = 0; i < k; i++)
                centres[i] = samples[i * samples.Count / k];
            var sums = new Vector4[k];
            for (int round = 0; round < ROUNDS; round++)
            {
                Array.Clear(sums, 0, k);
                foreach (Color c in samples)
                {
                    int n = Nearest(centres, c);
                    sums[n] += new Vector4(c.r, c.g, c.b, 1f);
                }

                for (int i = 0; i < k; i++)
                    if (sums[i].w > 0f)
                        centres[i] = new Color(sums[i].x / sums[i].w, sums[i].y / sums[i].w, sums[i].z / sums[i].w, 1f);
            }

            return centres;
        }

        private static int Nearest(Color[] colours, Color c)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < colours.Length; i++)
            {
                float dr = colours[i].r - c.r, dg = colours[i].g - c.g, db = colours[i].b - c.b;
                float d = dr * dr + dg * dg + db * db;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            return best;
        }
    }
}
