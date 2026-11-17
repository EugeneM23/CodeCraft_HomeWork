using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Turns a floor cube into mud: a dense grid child (MudGround) drawn with Game/MudGround, a
    /// MudSurface on the cube itself (where the wheels' casts find it) and the cube's own renderer
    /// switched off. The cube keeps its box collider, so the floor's physics stay as they were.
    ///
    /// The grid is a unit square on the cube's top face; the cube's scale stretches it to the
    /// floor. One vertex every <see cref="GRID_STEP"/> metres.
    /// </summary>
    public static class MudGroundBuilder
    {
        private const string MESH_PATH = "Assets/Game/Models/MudGround.asset";
        private const string MATERIAL_PATH = "Assets/Game/Materials/MT_MudGround.mat";
        private const string CHILD = "MudGround";
        private const float GRID_STEP = 0.08f;

        /// <summary>Highest the shader lifts the mud (m): thickness, relief and berm, with room to spare.</summary>
        private const float MAX_LIFT = 0.55f;

        [MenuItem("Tools/Playground/Mud Ground")]
        public static void ApplyToOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject floor = GameObject.Find("<Box>/Floor");
            if (floor == null)
            {
                Debug.LogError("[MUD] no <Box>/Floor in the open scene");
                return;
            }

            Apply(floor.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MUD] mud ground applied to {scene.path}");
        }

        /// <summary>
        /// Takes the mud off the open scene's floor: its mesh, its clumps and its MudSurface go,
        /// the cube's own renderer comes back. The mud's code and assets stay in the project.
        /// </summary>
        [MenuItem("Tools/Playground/Remove Mud Ground")]
        public static void RemoveFromOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject floor = GameObject.Find("<Box>/Floor");
            if (floor == null)
            {
                Debug.LogError("[MUD] no <Box>/Floor in the open scene");
                return;
            }

            Remove(floor.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MUD] mud ground removed from {scene.path}");
        }

        public static void Remove(Transform floor)
        {
            Transform child = floor.Find(CHILD);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);

            Transform clumps = floor.parent != null ? floor.parent.Find(CLUMPS) : null;
            if (clumps != null)
                Object.DestroyImmediate(clumps.gameObject);

            if (floor.TryGetComponent(out MudSurface surface))
                Object.DestroyImmediate(surface);

            if (floor.TryGetComponent(out MeshRenderer cubeRenderer))
                cubeRenderer.enabled = true;
        }

        public static void Apply(Transform floor)
        {
            Vector3 size = floor.lossyScale;
            Mesh mesh = BuildMesh(Mathf.Max(1, Mathf.RoundToInt(size.x / GRID_STEP)),
                Mathf.Max(1, Mathf.RoundToInt(size.z / GRID_STEP)));
            Material material = LoadOrCreateMaterial();

            Transform child = floor.Find(CHILD);
            if (child == null)
            {
                child = new GameObject(CHILD).transform;
                child.SetParent(floor, false);
            }

            // Top face of a unit cube.
            child.localPosition = new Vector3(0f, 0.5f, 0f);
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            child.gameObject.layer = floor.gameObject.layer;

            MeshFilter filter = GetOrAdd<MeshFilter>(child.gameObject);
            filter.sharedMesh = mesh;
            MeshRenderer renderer = GetOrAdd<MeshRenderer>(child.gameObject);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (floor.TryGetComponent(out MeshRenderer cubeRenderer))
                cubeRenderer.enabled = false;

            ParticleSystem clumps = BuildClumps(floor, out Transform clumpFloor);

            MudSurface surface = GetOrAdd<MudSurface>(floor.gameObject);
            SerializedObject serialized = new(surface);
            serialized.FindProperty("_renderer").objectReferenceValue = renderer;
            serialized.FindProperty("_stampShader").objectReferenceValue = Shader.Find("Hidden/Game/MudStamp");
            serialized.FindProperty("_clumps").objectReferenceValue = clumps;
            serialized.FindProperty("_clumpFloor").objectReferenceValue = clumpFloor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ clumps

        private const string CLUMPS = "MudClumps";
        private const string CLUMP_MESH_PATH = "Assets/Game/Models/MudClump.asset";
        private const string CLUMP_MATERIAL_PATH = "Assets/Game/Materials/MT_MudClump.mat";

        /// <summary>
        /// The clumps MudSurface.Throw emits: solid lumpy chunks (a mesh, not billboards) that fall,
        /// stop dead on a plane just under the mud's surface (MudSurface moves it there), lie there
        /// a few seconds and then slump — flatten first, then sink away. A sibling of the floor,
        /// not a child: the floor's scale would stretch them.
        /// </summary>
        private static ParticleSystem BuildClumps(Transform floor, out Transform plane)
        {
            Transform parent = floor.parent;
            Transform root = parent != null ? parent.Find(CLUMPS) : null;
            if (root == null)
            {
                root = new GameObject(CLUMPS).transform;
                root.SetParent(parent, false);
            }

            root.position = new Vector3(floor.position.x, 0f, floor.position.z);
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one;

            plane = root.Find("Surface");
            if (plane == null)
            {
                plane = new GameObject("Surface").transform;
                plane.SetParent(root, false);
            }
            plane.localPosition = Vector3.zero;
            plane.localRotation = Quaternion.identity;

            ParticleSystem system = GetOrAdd<ParticleSystem>(root.gameObject);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = true;
            main.startSpeed = 0f;
            main.startLifetime = 9f;
            main.startSize3D = true;
            main.startSize = 0.18f;
            main.startRotation3D = true;
            // Three times gravity: heavy wet mud, dropping fast in a short arc.
            main.gravityModifier = 3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 2500;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, plane);
            // A clod stops dead where it lands and stays: no bounce, no slide, no lifetime lost.
            // The mesh's pivot is near its bottom, so a near-zero radius rests it on the plane.
            collision.dampen = 1f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.05f;
            collision.minKillSpeed = 0f;

            // Melting: holds its shape most of its life, then slumps — the height goes first,
            // spreading a little as it does, and it sinks into the mud.
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            AnimationCurve wide = new(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(0.85f, 1.12f),
                new Keyframe(1f, 0f));
            AnimationCurve tall = new(new Keyframe(0f, 1f), new Keyframe(0.5f, 1f), new Keyframe(0.85f, 0.35f),
                new Keyframe(1f, 0f));
            size.x = new ParticleSystem.MinMaxCurve(1f, wide);
            size.y = new ParticleSystem.MinMaxCurve(1f, tall);
            size.z = new ParticleSystem.MinMaxCurve(1f, wide);

            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = false;

            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = LoadOrCreateClumpMesh();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = LoadOrCreateClumpMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            return system;
        }

        /// <summary>
        /// A lumpy, low-poly chunk of mud about a unit across: a coarse sphere pushed in and out by
        /// noise, flattened underneath, its pivot low so it rests sunk a little into what it lands on.
        /// </summary>
        private static Mesh LoadOrCreateClumpMesh()
        {
            const int rings = 6;
            const int segments = 9;
            System.Collections.Generic.List<Vector3> vertices = new();
            System.Collections.Generic.List<int> triangles = new();

            Random.State state = Random.state;
            Random.InitState(1729);
            // Torn, not rounded: big swings in radius, and a ragged rim where it spread on landing.
            float[] bumps = new float[(rings + 1) * segments];
            for (int i = 0; i < bumps.Length; i++)
                bumps[i] = Random.Range(0.55f, 1.35f);
            Random.state = state;

            vertices.Add(new Vector3(0.08f, 0.32f, -0.05f));
            for (int r = 1; r < rings; r++)
            {
                float polar = Mathf.PI * r / rings;
                for (int s = 0; s < segments; s++)
                {
                    float azimuth = Mathf.PI * 2f * (s + (r % 2) * 0.5f) / segments;
                    float radius = 0.5f * bumps[r * segments + s];
                    Vector3 p = new(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar),
                        Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    p *= radius;
                    // A low splat, not a ball: squashed, and flat underneath where it hit the ground.
                    p.y *= 0.55f;
                    if (p.y < -0.05f)
                        p.y = Mathf.Lerp(p.y, -0.05f, 0.8f);
                    p.y += 0.06f;
                    vertices.Add(p);
                }
            }
            vertices.Add(new Vector3(0f, 0f, 0f));

            int bottom = vertices.Count - 1;
            for (int s = 0; s < segments; s++)
            {
                triangles.Add(0);
                triangles.Add(1 + (s + 1) % segments);
                triangles.Add(1 + s);
            }
            for (int r = 0; r < rings - 2; r++)
            for (int s = 0; s < segments; s++)
            {
                int a = 1 + r * segments + s;
                int b = 1 + r * segments + (s + 1) % segments;
                int c = 1 + (r + 1) * segments + s;
                int d = 1 + (r + 1) * segments + (s + 1) % segments;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
                triangles.Add(c);
            }
            int last = 1 + (rings - 2) * segments;
            for (int s = 0; s < segments; s++)
            {
                triangles.Add(bottom);
                triangles.Add(last + s);
                triangles.Add(last + (s + 1) % segments);
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CLUMP_MESH_PATH);
            bool created = mesh == null;
            if (created)
                mesh = new Mesh();

            // Smooth-shaded like the ground it came from: hard facets made the clumps read as
            // rocks set on the mud rather than mud. The torn outline keeps it from being a blob.
            mesh.Clear();
            mesh.name = "MudClump";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (created)
                AssetDatabase.CreateAsset(mesh, CLUMP_MESH_PATH);
            else
                EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static Material LoadOrCreateClumpMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CLUMP_MATERIAL_PATH);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit")) { name = "MT_MudClump" };
                AssetDatabase.CreateAsset(material, CLUMP_MATERIAL_PATH);
            }

            // Opaque and untextured: the mesh's shape and the particle colour make the chunk.
            material.SetTexture("_BaseMap", null);
            material.SetFloat("_Surface", 0f);
            // The ground's own sheen, wet mud between its wet and dry: glossier read as chocolate.
            material.SetFloat("_Smoothness", 0.3f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component =>
            target.TryGetComponent(out T existing) ? existing : target.AddComponent<T>();

        private static Mesh BuildMesh(int cellsX, int cellsZ)
        {
            int columns = cellsX + 1;
            int rows = cellsZ + 1;
            Vector3[] vertices = new Vector3[columns * rows];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector4[] tangents = new Vector4[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];

            for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                int i = z * columns + x;
                float u = (float)x / cellsX;
                float v = (float)z / cellsZ;
                vertices[i] = new Vector3(u - 0.5f, 0f, v - 0.5f);
                normals[i] = Vector3.up;
                tangents[i] = new Vector4(1f, 0f, 0f, -1f);
                uvs[i] = new Vector2(u, v);
            }

            int[] triangles = new int[cellsX * cellsZ * 6];
            int t = 0;
            for (int z = 0; z < cellsZ; z++)
            for (int x = 0; x < cellsX; x++)
            {
                int a = z * columns + x;
                int b = a + columns;
                triangles[t++] = a;
                triangles[t++] = b;
                triangles[t++] = a + 1;
                triangles[t++] = a + 1;
                triangles[t++] = b;
                triangles[t++] = b + 1;
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MESH_PATH);
            bool created = mesh == null;
            if (created)
                mesh = new Mesh();

            mesh.Clear();
            mesh.name = "MudGround";
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            // The shader lifts every vertex; the bounds must hold the lifted mud or it gets culled.
            mesh.bounds = new Bounds(new Vector3(0f, MAX_LIFT * 0.5f, 0f), new Vector3(1f, MAX_LIFT, 1f));

            if (created)
                AssetDatabase.CreateAsset(mesh, MESH_PATH);
            else
                EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static Material LoadOrCreateMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Game/MudGround")) { name = "MT_MudGround" };
            AssetDatabase.CreateAsset(material, MATERIAL_PATH);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
