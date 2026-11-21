using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Pieces of our own ground for the worm (the user: not cubes, our ground with its look): a slab or a lump built of
    /// 0.1 m voxels with the terrain's material (Game/Voxel, vertex colours). A slab's top takes the ground's colour where
    /// it was torn out (grass, road, mud), its sides and underside the soil a little deeper; its outline is broken
    /// unevenly. Only the outer faces are built.
    /// </summary>
    public static class VoxelChunkMesh
    {
        private const float DEPTH = 0.35f; // m under the surface the soil colour is read

        private static Material s_material;
        private static readonly List<Vector3> s_vertices = new();
        private static readonly List<Vector3> s_normals = new();
        private static readonly List<Color32> s_colors = new();
        private static readonly List<int> s_triangles = new();

        /// <summary>
        /// A slab <paramref name="size"/> m (x across, y thick, z long) torn out of the ground where it lay flat at
        /// <paramref name="flatCentre"/> turned by <paramref name="flatRotation"/>. Its pivot is its middle.
        /// </summary>
        public static Transform Slab(Vector3 size, Vector3 flatCentre, Quaternion flatRotation, Transform parent)
        {
            VoxelBody ground = TerrainAt(flatCentre, out float voxel);
            int nx = Mathf.Max(2, Mathf.RoundToInt(size.x / voxel));
            int ny = Mathf.Max(1, Mathf.RoundToInt(size.y / voxel));
            int nz = Mathf.Max(2, Mathf.RoundToInt(size.z / voxel));

            // The outline: the corners and edges bitten off unevenly, the way broken asphalt splits.
            bool[,] column = new bool[nx, nz];
            float cut0 = Random.Range(0.1f, 0.35f), cut1 = Random.Range(0.1f, 0.35f);
            float cut2 = Random.Range(0.1f, 0.35f), cut3 = Random.Range(0.1f, 0.35f);
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                float u = x / (float)(nx - 1), v = z / (float)(nz - 1);
                // A diagonal chop off each corner, and a little ragged noise along the edges.
                bool corner = u + v < cut0 || (1f - u) + v < cut1 || u + (1f - v) < cut2 || (1f - u) + (1f - v) < cut3;
                bool edge = (x == 0 || z == 0 || x == nx - 1 || z == nz - 1) && Random.value < 0.35f;
                column[x, z] = !corner && !edge;
            }

            Color32[,] top = new Color32[nx, nz];
            Color32[,] soil = new Color32[nx, nz];
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                Vector3 local = new((x + 0.5f) * voxel - nx * voxel * 0.5f, 0f, (z + 0.5f) * voxel - nz * voxel * 0.5f);
                Vector3 world = flatCentre + flatRotation * local;
                top[x, z] = Sample(ground, world + Vector3.up * 0.05f, new Color32(70, 66, 60, 255));
                soil[x, z] = Sample(ground, world + Vector3.down * DEPTH, Darker(top[x, z]));
            }

            Begin();
            Vector3 half = new(nx * voxel * 0.5f, ny * voxel * 0.5f, nz * voxel * 0.5f);
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                if (!column[x, z])
                    continue;
                for (int y = 0; y < ny; y++)
                {
                    Vector3 min = new Vector3(x, y, z) * voxel - half;
                    Color32 c = y == ny - 1 ? top[x, z] : soil[x, z];
                    bool up = y == ny - 1, down = y == 0;
                    bool left = x == 0 || !column[x - 1, z], right = x == nx - 1 || !column[x + 1, z];
                    bool back = z == 0 || !column[x, z - 1], front = z == nz - 1 || !column[x, z + 1];
                    // The sides show the soil all the way up: the top layer's edge is the grass's own colour only on top.
                    Cube(min, voxel, c, up, down, left, right, back, front, soil[x, z]);
                }
            }
            return Finish("[GroundSlab]", parent, ground);
        }

        /// <summary>A lump of soil about <paramref name="size"/> m across, its colour from under the ground at <paramref name="at"/>.</summary>
        public static Transform Lump(float size, Vector3 at, Transform parent)
        {
            VoxelBody ground = TerrainAt(at, out float voxel);
            Color32 soil = Sample(ground, at + Vector3.down * DEPTH, new Color32(78, 60, 42, 255));
            Color32 top = Sample(ground, at + Vector3.up * 0.05f, soil);
            int n = Mathf.Max(3, Mathf.RoundToInt(size / voxel));
            float r = n * 0.5f;
            Vector3 stretch = new(Random.Range(0.8f, 1.2f), Random.Range(0.6f, 1f), Random.Range(0.8f, 1.3f));
            bool[,,] solid = new bool[n, n, n];
            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            for (int z = 0; z < n; z++)
            {
                Vector3 d = new((x + 0.5f - r) / stretch.x, (y + 0.5f - r) / stretch.y, (z + 0.5f - r) / stretch.z);
                solid[x, y, z] = d.magnitude < r * Random.Range(0.75f, 1f);
            }

            Begin();
            Vector3 half = Vector3.one * (n * voxel * 0.5f);
            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            for (int z = 0; z < n; z++)
            {
                if (!solid[x, y, z])
                    continue;
                bool Empty(int a, int b, int c) => a < 0 || b < 0 || c < 0 || a >= n || b >= n || c >= n || !solid[a, b, c];
                // A bit of turf left on one side of some lumps.
                Color32 c0 = y >= n - 2 && Random.value < 0.5f ? top : Vary(soil);
                Cube(new Vector3(x, y, z) * voxel - half, voxel, c0, Empty(x, y + 1, z), Empty(x, y - 1, z),
                    Empty(x - 1, y, z), Empty(x + 1, y, z), Empty(x, y, z - 1), Empty(x, y, z + 1), c0);
            }
            return Finish("[GroundLump]", parent, ground);
        }

        // ---- As real voxel bodies (the user: our ground, and the tank breaks it) ----

        /// <summary>
        /// The slab of <see cref="Slab"/> as a voxel body of our ground: standing (anchored on its bottom layer), with
        /// colliders, so the tank rams through it, shells and blasts bite it, and pieces knocked off fall as rubble.
        /// Its pivot is its middle.
        /// </summary>
        public static Transform SlabBody(Vector3 size, Vector3 flatCentre, Quaternion flatRotation, Transform parent)
        {
            VoxelBody ground = TerrainAt(flatCentre, out float voxel);
            int nx = Mathf.Max(2, Mathf.RoundToInt(size.x / voxel));
            int ny = Mathf.Max(1, Mathf.RoundToInt(size.y / voxel));
            int nz = Mathf.Max(2, Mathf.RoundToInt(size.z / voxel));
            bool[,] column = Outline(nx, nz);

            var palette = new Unity.Collections.NativeArray<Color32>(256, Unity.Collections.Allocator.Persistent);
            var index = new Dictionary<int, byte>();
            var voxels = new Unity.Collections.NativeArray<byte>(nx * ny * nz, Unity.Collections.Allocator.Persistent);
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                if (!column[x, z])
                    continue;
                Vector3 local = new((x + 0.5f) * voxel - nx * voxel * 0.5f, 0f, (z + 0.5f) * voxel - nz * voxel * 0.5f);
                Vector3 world = flatCentre + flatRotation * local;
                Color32 top = Sample(ground, world + Vector3.up * 0.05f, new Color32(70, 66, 60, 255));
                Color32 soil = Sample(ground, world + Vector3.down * DEPTH, Darker(top));
                for (int y = 0; y < ny; y++)
                    voxels[x + nx * (y + ny * z)] = Entry(palette, index, y == ny - 1 ? top : soil);
            }
            return Body("[GroundSlab]", new Unity.Mathematics.int3(nx, ny, nz), voxels, palette, voxel, parent);
        }

        /// <summary>The lump of <see cref="Lump"/> as a voxel body of our ground (see <see cref="SlabBody"/>).</summary>
        public static Transform LumpBody(float size, Vector3 at, Transform parent)
        {
            VoxelBody ground = TerrainAt(at, out float voxel);
            Color32 soil = Sample(ground, at + Vector3.down * DEPTH, new Color32(78, 60, 42, 255));
            Color32 top = Sample(ground, at + Vector3.up * 0.05f, soil);
            int n = Mathf.Max(3, Mathf.RoundToInt(size / voxel));
            float r = n * 0.5f;
            Vector3 stretch = new(Random.Range(0.8f, 1.2f), Random.Range(0.6f, 1f), Random.Range(0.8f, 1.3f));

            var palette = new Unity.Collections.NativeArray<Color32>(256, Unity.Collections.Allocator.Persistent);
            var index = new Dictionary<int, byte>();
            var voxels = new Unity.Collections.NativeArray<byte>(n * n * n, Unity.Collections.Allocator.Persistent);
            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            for (int z = 0; z < n; z++)
            {
                Vector3 d = new((x + 0.5f - r) / stretch.x, (y + 0.5f - r) / stretch.y, (z + 0.5f - r) / stretch.z);
                if (d.magnitude >= r * Random.Range(0.75f, 1f))
                    continue;
                Color32 c = y >= n - 2 && Random.value < 0.5f ? top : Vary(soil);
                voxels[x + n * (y + n * z)] = Entry(palette, index, c);
            }
            return Body("[GroundLump]", new Unity.Mathematics.int3(n, n, n), voxels, palette, voxel, parent);
        }

        private static Transform Body(string name, Unity.Mathematics.int3 size, Unity.Collections.NativeArray<byte> voxels,
            Unity.Collections.NativeArray<Color32> palette, float voxel, Transform parent)
        {
            VoxelBodySettings settings = VoxelBodySettings.Default(voxel);
            settings.Origin = new Unity.Mathematics.float3(-size.x * voxel * 0.5f, -size.y * voxel * 0.5f, -size.z * voxel * 0.5f);
            // Standing on its bottom layer, like a wall: the tank rams it, bites leave it standing, cut-off bits fall.
            settings.Anchor = VoxelAnchor.Ground;
            settings.Colliders = true;
            settings.Layer = 0;
            settings.CraterDepth = 0.8f;
            settings.ColliderCell = 2;
            // The ground's own material: the shared one (the houses') showed its colours pale grey.
            settings.Material = s_material;

            GameObject go = new(name) { layer = 0 };
            go.transform.SetParent(parent, false);
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            return go.transform;
        }

        // The slab's outline: corners chopped off diagonally, the edges ragged.
        private static bool[,] Outline(int nx, int nz)
        {
            bool[,] column = new bool[nx, nz];
            float cut0 = Random.Range(0.1f, 0.35f), cut1 = Random.Range(0.1f, 0.35f);
            float cut2 = Random.Range(0.1f, 0.35f), cut3 = Random.Range(0.1f, 0.35f);
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                float u = x / (float)(nx - 1), v = z / (float)(nz - 1);
                bool corner = u + v < cut0 || (1f - u) + v < cut1 || u + (1f - v) < cut2 || (1f - u) + (1f - v) < cut3;
                bool edge = (x == 0 || z == 0 || x == nx - 1 || z == nz - 1) && Random.value < 0.35f;
                column[x, z] = !corner && !edge;
            }
            return column;
        }

        // A palette entry for a colour: colours a few shades apart share one (255 entries at most).
        private static byte Entry(Unity.Collections.NativeArray<Color32> palette, Dictionary<int, byte> index, Color32 c)
        {
            int key = (c.r >> 2) << 12 | (c.g >> 2) << 6 | (c.b >> 2);
            if (index.TryGetValue(key, out byte i))
                return i;
            if (index.Count >= 255)
                return 1;
            byte next = (byte)(index.Count + 1);
            palette[next] = c;
            index[key] = next;
            return next;
        }

        private static VoxelBody TerrainAt(Vector3 at, out float voxel)
        {
            IReadOnlyList<VoxelBody> bodies = VoxelWorld.Bodies;
            VoxelBody best = null;
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody b = bodies[i];
                if (b == null || !b.IsCreated || !b.IsTerrain)
                    continue;
                Bounds bounds = b.WorldBounds;
                if (at.x >= bounds.min.x && at.x <= bounds.max.x && at.z >= bounds.min.z && at.z <= bounds.max.z)
                {
                    best = b;
                    break;
                }
                best ??= b;
            }
            voxel = best != null ? best.VoxelSize * Mathf.Abs(best.transform.lossyScale.x) : 0.1f;
            if (s_material == null && best != null)
            {
                MeshRenderer r = best.GetComponentInChildren<MeshRenderer>();
                if (r != null)
                    s_material = r.sharedMaterial;
            }
            return best;
        }

        private static Color32 Sample(VoxelBody ground, Vector3 at, Color32 fallback) =>
            ground != null && ground.TryGetColor(at, out Color32 c) ? c : fallback;

        private static Color32 Darker(Color32 c) => new((byte)(c.r * 0.7f), (byte)(c.g * 0.7f), (byte)(c.b * 0.7f), 255);

        private static Color32 Vary(Color32 c)
        {
            float k = Random.Range(0.85f, 1.1f);
            return new Color32((byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), 255);
        }

        private static void Begin()
        {
            s_vertices.Clear();
            s_normals.Clear();
            s_colors.Clear();
            s_triangles.Clear();
        }

        private static Transform Finish(string name, Transform parent, VoxelBody ground)
        {
            Mesh mesh = new() { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(s_vertices);
            mesh.SetNormals(s_normals);
            mesh.SetColors(s_colors);
            mesh.SetTriangles(s_triangles, 0);
            mesh.RecalculateBounds();

            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = s_material != null ? s_material : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            go.AddComponent<DestroyMeshWithObject>().Mesh = mesh;
            return go.transform;
        }

        // The faces of one voxel that are open; the top face in c, the rest in side.
        private static void Cube(Vector3 min, float s, Color32 c, bool up, bool down, bool left, bool right, bool back,
            bool front, Color32 side)
        {
            Vector3 a = min, b = min + Vector3.one * s;
            if (up) Quad(new(a.x, b.y, a.z), new(a.x, b.y, b.z), new(b.x, b.y, b.z), new(b.x, b.y, a.z), Vector3.up, c);
            if (down) Quad(new(a.x, a.y, a.z), new(b.x, a.y, a.z), new(b.x, a.y, b.z), new(a.x, a.y, b.z), Vector3.down, side);
            if (left) Quad(new(a.x, a.y, a.z), new(a.x, a.y, b.z), new(a.x, b.y, b.z), new(a.x, b.y, a.z), Vector3.left, side);
            if (right) Quad(new(b.x, a.y, a.z), new(b.x, b.y, a.z), new(b.x, b.y, b.z), new(b.x, a.y, b.z), Vector3.right, side);
            if (back) Quad(new(a.x, a.y, a.z), new(a.x, b.y, a.z), new(b.x, b.y, a.z), new(b.x, a.y, a.z), Vector3.back, side);
            if (front) Quad(new(a.x, a.y, b.z), new(b.x, a.y, b.z), new(b.x, b.y, b.z), new(a.x, b.y, b.z), Vector3.forward, side);
        }

        private static void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n, Color32 c)
        {
            int i = s_vertices.Count;
            s_vertices.Add(p0);
            s_vertices.Add(p1);
            s_vertices.Add(p2);
            s_vertices.Add(p3);
            for (int k = 0; k < 4; k++)
            {
                s_normals.Add(n);
                s_colors.Add(c);
            }
            s_triangles.Add(i);
            s_triangles.Add(i + 1);
            s_triangles.Add(i + 2);
            s_triangles.Add(i);
            s_triangles.Add(i + 2);
            s_triangles.Add(i + 3);
        }

        /// <summary>Frees the built mesh with the piece.</summary>
        private sealed class DestroyMeshWithObject : MonoBehaviour
        {
            public Mesh Mesh;

            private void OnDestroy()
            {
                if (Mesh != null)
                    Destroy(Mesh);
            }
        }
    }
}
