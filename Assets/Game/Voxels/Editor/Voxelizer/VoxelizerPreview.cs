using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Voxels.EditorTools
{
    /// <summary>
    /// The Voxelizer window's two panels without Play: the model as it is and its voxel version,
    /// each in its own PreviewRenderUtility scene, one orbit camera for both.
    /// </summary>
    public sealed class VoxelizerPreview : IDisposable
    {
        private const int CHUNK = 16;
        private const float FOV = 30f;
        private const float DRAG_DEGREES = 0.5f;
        private static readonly Color BACKGROUND = new(0.55f, 0.6f, 0.66f);
        private static readonly Color AMBIENT = new(0.32f, 0.33f, 0.36f);
        private static readonly int3[] DIRECTIONS =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)
        };

        private readonly List<Mesh> _meshes = new();
        private PreviewRenderUtility _source;
        private PreviewRenderUtility _voxels;
        private Material _material;
        private Bounds _frame;
        private float _yaw = 140f;
        private float _pitch = 22f;
        private float _zoom = 1f;

        public bool HasModel => _source != null;
        /// <summary>One line: parts, voxels, non-empty 16³ chunks, voxelization time.</summary>
        public string Summary { get; private set; } = "";
        /// <summary>Voxels per part, one line each.</summary>
        public string PartCounts { get; private set; } = "";

        /// <summary>Voxelizes a fresh copy of the config's model and shows it beside the model as it is.</summary>
        public void Recompute(VoxelizerConfig config)
        {
            if (config == null || config.model == null)
                throw new ArgumentException("The config has no model.");
            Clear();
            if (_material == null)
            {
                Shader shader = Shader.Find("Game/Voxel");
                if (shader == null) throw new InvalidOperationException("Shader 'Game/Voxel' not found.");
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            _source = NewUtility();
            _voxels = NewUtility();
            GameObject model = _source.InstantiatePrefabInScene(config.model);
            GameObject instance = _voxels.InstantiatePrefabInScene(config.model);
            _frame = BoundsOf(model);

            var watch = Stopwatch.StartNew();
            using VoxelizerResult result = VoxelizeUseCase.Run(config, instance);
            watch.Stop();

            // The voxels take the place of the renderers their parts own, as in the baked prefab.
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                if (Owner(r.transform, instance.transform, result) is { } owner && result.Parts.Exists(p => p.Root == owner))
                    r.enabled = false;

            var counts = new StringBuilder();
            int total = 0, chunks = 0;
            foreach (VoxelizerResultPart part in result.Parts)
            {
                Mesh mesh = BuildMesh(part.Size, part.Voxels, result.Palette, result.VoxelSize, part.Origin);
                mesh.hideFlags = HideFlags.HideAndDontSave;
                _meshes.Add(mesh);
                GameObject go = EditorUtility.CreateGameObjectWithHideFlags("Voxels", HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
                SceneManager.MoveGameObjectToScene(go, instance.scene);
                go.transform.SetParent(part.Root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = _material;

                int solid = Count(part.Voxels, part.Size, out int used);
                total += solid;
                chunks += used;
                counts.AppendLine($"{(part.Path == "" ? "(root)" : part.Path)}: {solid:N0}  ({part.Size.x}×{part.Size.y}×{part.Size.z})");
            }

            float voxelM = result.VoxelSize * result.Scale;
            Summary = $"{result.Parts.Count} parts, {total:N0} voxels, {chunks} chunks 16³, voxel {voxelM * 100f:0.#} cm, "
                      + $"voxelized in {watch.ElapsedMilliseconds} ms";
            PartCounts = counts.ToString();
        }

        /// <summary>Draws both panels; returns true when the mouse turned or zoomed the camera (repaint).</summary>
        public bool Render(Rect source, Rect voxels)
        {
            if (_source == null) return false;
            bool moved = Orbit(source) | Orbit(voxels);
            if (Event.current.type == EventType.Repaint)
            {
                Draw(_source, source);
                Draw(_voxels, voxels);
            }

            return moved;
        }

        public void Dispose()
        {
            Clear();
            if (_material != null) Object.DestroyImmediate(_material);
            _material = null;
        }

        /// <summary>
        /// Exposed faces of a byte grid (palette indices, 0 empty), in the grid's space: a face only
        /// between a full voxel and an empty one (or the grid's edge). Vertex colours from the palette.
        /// </summary>
        public static Mesh BuildMesh(int3 size, NativeArray<byte> voxels, Color32[] palette, float voxelSize, float3 origin)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colours = new List<Color32>();
            var triangles = new List<int>();
            float half = voxelSize * 0.5f;
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                byte index = voxels[x + size.x * (y + size.y * z)];
                if (index == 0) continue;
                Color32 colour = palette[index];
                foreach (int3 d in DIRECTIONS)
                {
                    if (Solid(voxels, size, x + d.x, y + d.y, z + d.z)) continue;
                    // a = up for the ±X faces, right otherwise; b = n × a: (0, 1, 2) faces outwards.
                    var n = new Vector3(d.x, d.y, d.z);
                    Vector3 a = d.x != 0 ? Vector3.up : Vector3.right;
                    Vector3 b = Vector3.Cross(n, a);
                    Vector3 centre = (Vector3)origin + (new Vector3(x, y, z) + Vector3.one * 0.5f + n * 0.5f) * voxelSize;
                    int s = vertices.Count;
                    vertices.Add(centre + (-a - b) * half);
                    vertices.Add(centre + (a - b) * half);
                    vertices.Add(centre + (a + b) * half);
                    vertices.Add(centre + (-a + b) * half);
                    for (int k = 0; k < 4; k++)
                    {
                        normals.Add(n);
                        colours.Add(colour);
                    }

                    triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
                    triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 3);
                }
            }

            var mesh = new Mesh { name = "Voxelizer Preview", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool Solid(NativeArray<byte> voxels, int3 size, int x, int y, int z) =>
            x >= 0 && y >= 0 && z >= 0 && x < size.x && y < size.y && z < size.z && voxels[x + size.x * (y + size.y * z)] != 0;

        /// <summary>Full voxels, and how many 16³ chunks hold at least one.</summary>
        private static int Count(NativeArray<byte> voxels, int3 size, out int chunks)
        {
            int3 cs = (size + CHUNK - 1) / CHUNK;
            var used = new bool[cs.x * cs.y * cs.z];
            int solid = 0;
            chunks = 0;
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                if (voxels[x + size.x * (y + size.y * z)] == 0) continue;
                solid++;
                int c = x / CHUNK + cs.x * (y / CHUNK + cs.y * (z / CHUNK));
                if (used[c]) continue;
                used[c] = true;
                chunks++;
            }

            return solid;
        }

        /// <summary>The nearest part or kept root at or above a transform, as VoxelizeUseCase assigned it.</summary>
        private static Transform Owner(Transform from, Transform root, VoxelizerResult result)
        {
            for (Transform t = from; t != null; t = t == root ? null : t.parent)
            {
                if (result.Parts.Exists(p => p.Root == t) || result.KeptRoots.Contains(t))
                    return t;
            }

            return null;
        }

        private static Bounds BoundsOf(GameObject instance)
        {
            var bounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static PreviewRenderUtility NewUtility()
        {
            var utility = new PreviewRenderUtility();
            Camera camera = utility.camera;
            camera.fieldOfView = FOV;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BACKGROUND;
            utility.lights[0].intensity = 1.25f;
            utility.lights[0].transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            utility.lights[1].intensity = 0.45f;
            utility.lights[1].transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            utility.ambientColor = AMBIENT;
            return utility;
        }

        /// <summary>Left drag turns the camera round the model, the wheel moves it in and out.</summary>
        private bool Orbit(Rect rect)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition)) return false;
            switch (e.type)
            {
                case EventType.MouseDrag when e.button == 0:
                    _yaw += e.delta.x * DRAG_DEGREES;
                    _pitch = Mathf.Clamp(_pitch + e.delta.y * DRAG_DEGREES, -89f, 89f);
                    e.Use();
                    return true;
                case EventType.ScrollWheel:
                    _zoom = Mathf.Clamp(_zoom * (1f + e.delta.y * 0.05f), 0.1f, 5f);
                    e.Use();
                    return true;
                default:
                    return false;
            }
        }

        private void Draw(PreviewRenderUtility utility, Rect rect)
        {
            Camera camera = utility.camera;
            float distance = Mathf.Max(_frame.extents.magnitude, 0.01f) / Mathf.Sin(FOV * 0.5f * Mathf.Deg2Rad) * _zoom;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            camera.transform.SetPositionAndRotation(_frame.center - rotation * Vector3.forward * distance, rotation);
            camera.nearClipPlane = distance * 0.01f;
            camera.farClipPlane = distance * 10f;
            utility.BeginPreview(rect, GUIStyle.none);
            utility.Render(true);
            utility.EndAndDrawPreview(rect);
        }

        private void Clear()
        {
            _source?.Cleanup();
            _voxels?.Cleanup();
            _source = _voxels = null;
            foreach (Mesh mesh in _meshes)
                if (mesh != null) Object.DestroyImmediate(mesh);
            _meshes.Clear();
            Summary = PartCounts = "";
        }
    }
}
