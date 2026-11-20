using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.Horde.EditorTools
{
    /// <summary>
    /// Bakes a skinned monster for the horde: a low-poly copy of the skin (Unity Mesh LOD generation on a copy; the FBX
    /// import settings are not touched), its vertex animation texture (run, attack and idle sampled at 30 fps with a
    /// PlayableGraph and <see cref="SkinnedMeshRenderer.BakeMesh(Mesh, bool)"/>), the corpse mesh (last frame of the
    /// death clip), the material and <see cref="HordeArtConfig"/>, all in <c>Assets/Game/Horde/Art/</c>. Two sources:
    /// the Regax (SK_Rapax) and the low-poly zombie (SM_Chr_Zombie_Male_01 of the PolygonApocalypse characters, its
    /// humanoid clips in <c>Assets/Game/Animations/Zombie</c>). Re-baking overwrites the assets in place (their GUIDs stay).
    /// </summary>
    public static class HordeVatBaker
    {
        public const string OUTPUT_FOLDER = "Assets/Game/Horde/Art";
        public const string CONFIG_PATH = OUTPUT_FOLDER + "/RegaxHordeArtConfig.asset";
        public const string ZOMBIE_CONFIG_PATH = OUTPUT_FOLDER + "/ZombieHordeArtConfig.asset";

        private const string SHADER_NAME = "Game/HordeVAT";
        private const float FRAME_RATE = 30f;
        private const int MAX_TRIANGLES = 2000;
        private const int MAX_LODS = 8;

        /// <summary>Height of the Regax at render scale 1, m: the simulation's body constants were set for it.</summary>
        private const float REGAX_HEIGHT = 2.6f;

        private const string RAPAX_FOLDER = "Assets/3rdParty/Rapax/FBX Files/";
        private const string ZOMBIE_CLIPS = "Assets/Game/Animations/Zombie/";

        /// <summary>What to bake and where to.</summary>
        private sealed class Source
        {
            public string Name;
            public string ModelPath;
            /// <summary>The skin to bake (a child of the model); empty: the first one.</summary>
            public string SkinName;
            public System.Func<AnimationClip> Run, Attack, Idle, Death;
            public string ConfigPath;
            /// <summary>Height of the baked model at render scale 1, m; 0: as authored.</summary>
            public float Height;
            /// <summary>Albedo of the material (the zombie's atlas); null: none.</summary>
            public string AlbedoPath;
            /// <summary>Colour over the albedo (the crowd shader lights more simply than URP Lit: a little over white).</summary>
            public Color AlbedoColor = Color.white;
            /// <summary>Material of the source skin: its colour and emission are copied (the Regax).</summary>
            public string SourceMaterialPath;
            /// <summary>
            /// Walking speed the run clip is played for at render scale 1 (<see cref="HordeArtConfig.runClipSpeed"/>); 0 keeps
            /// the horde options' own. The clip's measured root speed is reported beside it.
            /// </summary>
            public float RunClipSpeed;
            /// <summary>Play the run clip for its own root speed (<see cref="RunClipSpeed"/> is then only the fallback): no foot sliding.</summary>
            public bool RunAtRootSpeed;
            /// <summary>Body radius and bite reach over the kinds' (<see cref="HordeArtConfig.reachScale"/>).</summary>
            public float ReachScale = 1f;
            /// <summary>Make a plain acid material apart from the textured crowd one.</summary>
            public bool AcidMaterial;
            public Color[] KindTints;
            public Color[] KindEmission;
        }

        private static Source Regax => new()
        {
            Name = "Regax",
            ModelPath = RAPAX_FOLDER + "SK_Rapax.fbx",
            Run = () => LoadFbxClip(RAPAX_FOLDER + "Rapax@Run.fbx"),
            Attack = () => LoadFbxClip(RAPAX_FOLDER + "Rapax@RightClawsAttack.fbx"),
            Idle = () => LoadFbxClip(RAPAX_FOLDER + "Rapax@IdleBreathe.fbx"),
            Death = () => LoadFbxClip(RAPAX_FOLDER + "Rapax@Death.fbx"),
            ConfigPath = CONFIG_PATH,
            SourceMaterialPath = "Assets/3rdParty/Rapax/Materials/M_Rapax 1.mat"
        };

        private static Source Zombie => new()
        {
            Name = "Zombie",
            ModelPath = "Assets/Game/Models/Zombie/SK_Zombie_Characters.fbx",
            SkinName = "SM_Chr_Zombie_Male_01",
            // The user's Mixamo run (a forward lurch, 0.8 s a loop), humanoid on its own avatar, root motion kept in the clip.
            Run = () => LoadFbxClip(ZOMBIE_CLIPS + "Zombie Running.fbx"),
            Attack = () => AssetDatabase.LoadAssetAtPath<AnimationClip>(ZOMBIE_CLIPS + "Zombie_Attack.anim"),
            Idle = () => AssetDatabase.LoadAssetAtPath<AnimationClip>(ZOMBIE_CLIPS + "Zombie_Idle.anim"),
            Death = () => AssetDatabase.LoadAssetAtPath<AnimationClip>(ZOMBIE_CLIPS + "Zombie_Death_Handgun.anim"),
            ConfigPath = ZOMBIE_CONFIG_PATH,
            // The crawler's render scale is 0.4 in the levels: 0.4 × 4.008 = a 1.6 m zombie (the user: twice 25 % smaller than the
            // 2.85 m one, which was half as big again as
            // the first 1.9 m), readable from the tank's camera. Its radius and bite reach grow with it (the kinds were
            // tuned on a 1.9 m one).
            Height = 4.0078125f,
            ReachScale = 0.84375f,
            AlbedoPath = ZombieAtlas.VIVID,
            AlbedoColor = new Color(1.1f, 1.1f, 1.1f, 1f),
            // The user: the zombie moves at the run's own root speed, the feet do not slide. The crawler's speed is set to it
            // (HordeKind.Crawler, the arena's options), and the clip plays at 1x when it moves that fast.
            RunClipSpeed = 6.75f,
            RunAtRootSpeed = true,
            AcidMaterial = true,
            // Row 1, the spitter: a sickly green over the atlas (the tint takes the material's white colour's place).
            KindTints = new[] { new Color(0f, 0f, 0f, 0f), new Color(0.62f, 0.85f, 0.3f, 1f) },
            KindEmission = new[] { Color.black, new Color(0.05f, 0.1f, 0f, 1f) }
        };

        [MenuItem("Tools/Horde/Bake Regax VAT")]
        public static void BakeRegaxFromMenu() => Debug.Log(Bake());

        [MenuItem("Tools/Horde/Bake Zombie VAT")]
        public static void BakeZombieFromMenu() => Debug.Log(BakeZombie());

        /// <summary>Bakes the Regax; returns a short report.</summary>
        public static string Bake() => Bake(Regax);

        /// <summary>Bakes the zombie; returns a short report.</summary>
        public static string BakeZombie()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(ZombieAtlas.VIVID) == null)
                ZombieAtlas.Make();
            return Bake(Zombie);
        }

        private static string Bake(Source src)
        {
            var report = new StringBuilder(src.Name).Append(": ");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(src.ModelPath);
            if (prefab == null)
                return "HordeVatBaker: no " + src.ModelPath;

            Scene scene = EditorSceneManager.NewPreviewScene();
            var graph = PlayableGraph.Create("HordeVatBaker");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                SkinnedMeshRenderer skin = FindSkin(instance, src.SkinName);
                if (skin == null)
                    return "HordeVatBaker: no skin " + src.SkinName + " in " + src.ModelPath;
                var animator = instance.GetComponentInChildren<Animator>();
                animator.applyRootMotion = false;
                animator.fireEvents = false;
                Mesh source = skin.sharedMesh;

                // ---------------------------------------------------------- decimation
                int[] sourceIndices = CutLod(source, out int lod, out int lodCount);
                var map = new int[source.vertexCount];
                for (int i = 0; i < map.Length; i++)
                    map[i] = -1;
                var used = new List<int>();
                var indices = new int[sourceIndices.Length];
                for (int k = 0; k < sourceIndices.Length; k++)
                {
                    int v = sourceIndices[k];
                    if (map[v] < 0)
                    {
                        map[v] = used.Count;
                        used.Add(v);
                    }

                    indices[k] = map[v];
                }

                int vertexCount = used.Count;
                int triangles = indices.Length / 3;
                report.AppendLine($"source {source.vertexCount} verts {source.triangles.Length / 3} tris; Mesh LOD {lod} of {lodCount}: {vertexCount} verts {triangles} tris");

                // ---------------------------------------------------------- clips
                AnimationClip run = src.Run();
                AnimationClip attack = src.Attack();
                AnimationClip idle = src.Idle();
                AnimationClip death = src.Death();
                if (run == null || attack == null || idle == null || death == null)
                    return "HordeVatBaker: a clip is missing";

                int runFrames = LoopFrames(run);
                int attackFrames = LoopFrames(attack);
                int idleFrames = LoopFrames(idle);
                int frames = runFrames + attackFrames + idleFrames;

                var output = AnimationPlayableOutput.Create(graph, "Bake", animator);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var baked = new Mesh();
                var pixels = new Color[vertexCount * frames * 2];
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var bounds = new Bounds();
                bool boundsSet = false;
                Vector3[] firstPositions = null;
                Vector3[] firstNormals = null;

                void Sample(AnimationClip clip, float time)
                {
                    // A humanoid clip does not move the bones through a manually evaluated graph in edit mode
                    // (the zombie stayed in its bind pose); SampleAnimation does.
                    if (clip.humanMotion)
                    {
                        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        clip.SampleAnimation(instance, time);
                        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        skin.BakeMesh(baked, true);
                        baked.GetVertices(vertices);
                        baked.GetNormals(normals);
                        return;
                    }

                    var playable = AnimationClipPlayable.Create(graph, clip);
                    playable.SetApplyFootIK(false);
                    output.SetSourcePlayable(playable);
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    playable.SetTime(time);
                    graph.Evaluate();
                    skin.BakeMesh(baked, true);
                    baked.GetVertices(vertices);
                    baked.GetNormals(normals);
                    playable.Destroy();
                }

                // Model scale: the height of the idle pose to src.Height (1 when the source keeps its size).
                float scale = 1f;
                if (src.Height > 0f)
                {
                    Sample(idle, 0f);
                    float top = float.MinValue;
                    float bottom = float.MaxValue;
                    for (int v = 0; v < vertexCount; v++)
                    {
                        float y = (skin.transform.rotation * vertices[used[v]] + skin.transform.position).y;
                        top = Mathf.Max(top, y);
                        bottom = Mathf.Min(bottom, Mathf.Min(y, 0f));
                    }

                    scale = src.Height / Mathf.Max(top - bottom, 0.01f);
                    report.AppendLine($"authored height {top - bottom:F2} m, scale {scale:F3}");
                }

                // World space of the baked skin (BakeMesh with scale gives the renderer's space minus its TRS), then the model scale.
                Vector3 ToModel(Vector3 p) => (skin.transform.rotation * p + skin.transform.position) * scale;
                Vector3 ToModelDir(Vector3 n) => (skin.transform.rotation * n).normalized;

                // A humanoid clip carries its root motion in the pose (the walk strides ~1.4 m forward a loop): the
                // hips' start and their straight drift over the loop are taken off, so the monster stays on its spot.
                Transform hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                Vector3 HipsXZ(AnimationClip clip, float time)
                {
                    Sample(clip, time);
                    Vector3 h = hips.position;
                    return new Vector3(h.x, 0f, h.z);
                }

                int row = 0;
                void BakeClip(AnimationClip clip, int count)
                {
                    Vector3 start = Vector3.zero;
                    Vector3 drift = Vector3.zero;
                    if (clip.humanMotion && hips != null)
                    {
                        start = HipsXZ(clip, 0f);
                        drift = (HipsXZ(clip, clip.length) - start) / clip.length;
                    }

                    for (int f = 0; f < count; f++, row++)
                    {
                        float time = f / FRAME_RATE;
                        Sample(clip, time);
                        Vector3 offset = (start + drift * time) * scale;
                        for (int v = 0; v < vertexCount; v++)
                        {
                            Vector3 p = ToModel(vertices[used[v]]) - offset;
                            Vector3 n = ToModelDir(normals[used[v]]);
                            pixels[row * vertexCount + v] = new Color(p.x, p.y, p.z, 1f);
                            pixels[(frames + row) * vertexCount + v] = new Color(n.x, n.y, n.z, 0f);
                            if (!boundsSet)
                            {
                                bounds = new Bounds(p, Vector3.zero);
                                boundsSet = true;
                            }
                            else
                            {
                                bounds.Encapsulate(p);
                            }
                        }

                        if (firstPositions == null)
                        {
                            firstPositions = new Vector3[vertexCount];
                            firstNormals = new Vector3[vertexCount];
                            for (int v = 0; v < vertexCount; v++)
                            {
                                firstPositions[v] = ToModel(vertices[used[v]]) - offset;
                                firstNormals[v] = ToModelDir(normals[used[v]]);
                            }
                        }
                    }
                }

                BakeClip(run, runFrames);
                Vector3 runDrift = ToVector(pixels[(runFrames - 1) * vertexCount]) - ToVector(pixels[0]);
                BakeClip(attack, attackFrames);
                BakeClip(idle, idleFrames);

                // Corpse: the last frame of the death clip. A humanoid fall carries the body ~1.4 m away from where it
                // stood, and a corpse off the death spot reads as a miss: its middle (XZ) goes on the spot.
                Sample(death, death.length);
                var corpsePositions = new Vector3[vertexCount];
                var corpseNormals = new Vector3[vertexCount];
                var corpseBounds = new Bounds(ToModel(vertices[used[0]]), Vector3.zero);
                for (int v = 0; v < vertexCount; v++)
                {
                    corpsePositions[v] = ToModel(vertices[used[v]]);
                    corpseNormals[v] = ToModelDir(normals[used[v]]);
                    corpseBounds.Encapsulate(corpsePositions[v]);
                }

                if (death.humanMotion)
                {
                    var middle = new Vector3(corpseBounds.center.x, 0f, corpseBounds.center.z);
                    for (int v = 0; v < vertexCount; v++)
                        corpsePositions[v] -= middle;
                }

                float rootSpeed = 0f;
                if (src.RunClipSpeed > 0f)
                {
                    rootSpeed = RootSpeed(graph, output, animator, instance.transform, run) * scale;
                    report.AppendLine($"run clip root speed {rootSpeed:F2} m/s at render scale 1, played for {src.RunClipSpeed:F2}");
                }

                Object.DestroyImmediate(baked);
                report.AppendLine($"frames run {runFrames} attack {attackFrames} idle {idleFrames}; VAT {vertexCount}x{frames * 2} RGBAHalf; bounds {bounds.center:F2} size {bounds.size:F2}; run vertex-0 drift {runDrift:F3}");

                // ---------------------------------------------------------- assets
                if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
                    AssetDatabase.CreateFolder("Assets/Game/Horde", "Art");

                var uv = new List<Vector2>();
                source.GetUVs(0, uv);
                var meshUv = new Vector2[vertexCount];
                var ids = new Vector2[vertexCount];
                for (int v = 0; v < vertexCount; v++)
                {
                    meshUv[v] = used[v] < uv.Count ? uv[used[v]] : Vector2.zero;
                    ids[v] = new Vector2(v, 0f);
                }

                string prefix = OUTPUT_FOLDER + "/";
                Mesh crowd = BuildMesh("SM_Horde" + src.Name, firstPositions, firstNormals, meshUv, ids, indices);
                crowd.bounds = bounds;
                crowd = SaveOrReplace(crowd, prefix + "SM_Horde" + src.Name + ".asset");

                Mesh corpse = BuildMesh("SM_Horde" + src.Name + "_Corpse", corpsePositions, corpseNormals, meshUv, ids, indices);
                corpse = SaveOrReplace(corpse, prefix + "SM_Horde" + src.Name + "_Corpse.asset");

                var vat = new Texture2D(vertexCount, frames * 2, TextureFormat.RGBAHalf, false, true)
                {
                    name = "T_Horde" + src.Name + "_VAT",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                vat.SetPixels(pixels);
                vat.Apply(false, false);
                vat = SaveOrReplace(vat, prefix + "T_Horde" + src.Name + "_VAT.asset");
                SetNotReadable(crowd);
                SetNotReadable(corpse);
                SetNotReadable(vat);

                Material material = SaveMaterial(src, prefix + "MT_Horde" + src.Name + ".mat", vat, frames, true);
                Material acid = src.AcidMaterial ? SaveMaterial(src, prefix + "MT_Horde" + src.Name + "_Acid.mat", vat, frames, false) : null;

                var config = AssetDatabase.LoadAssetAtPath<HordeArtConfig>(src.ConfigPath);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<HordeArtConfig>();
                    if (src.KindTints != null)
                        config.kindTints = src.KindTints;
                    if (src.KindEmission != null)
                        config.kindEmission = src.KindEmission;
                    AssetDatabase.CreateAsset(config, src.ConfigPath);
                }

                config.mesh = crowd;
                config.corpseMesh = corpse;
                config.material = material;
                config.acidMaterial = acid;
                config.vat = vat;
                config.vertexCount = vertexCount;
                config.frameCount = frames;
                config.run = new HordeClip { start = 0, count = runFrames, frameRate = FRAME_RATE, loop = true };
                config.attack = new HordeClip { start = runFrames, count = attackFrames, frameRate = FRAME_RATE, loop = true };
                config.idle = new HordeClip { start = runFrames + attackFrames, count = idleFrames, frameRate = FRAME_RATE, loop = true };
                config.bounds = bounds;
                config.triangles = triangles;
                config.sourceTriangles = source.triangles.Length / 3;
                config.meshLod = lod;
                config.bodyScale = src.Height > 0f ? src.Height / REGAX_HEIGHT : 1f;
                config.runClipSpeed = src.RunAtRootSpeed && rootSpeed > 0.1f ? rootSpeed : src.RunClipSpeed;
                config.reachScale = src.ReachScale;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                report.Append("saved to ").Append(src.ConfigPath);
                return report.ToString();
            }
            finally
            {
                if (graph.IsValid())
                    graph.Destroy();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static SkinnedMeshRenderer FindSkin(GameObject instance, string name)
        {
            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (string.IsNullOrEmpty(name) || skin.name == name)
                    return skin;
            return null;
        }

        /// <summary>
        /// Average horizontal root speed of <paramref name="clip"/> over one loop, m/s of the source model: the clip
        /// played with root motion on the model's transform.
        /// </summary>
        private static float RootSpeed(PlayableGraph graph, AnimationPlayableOutput output, Animator animator, Transform root, AnimationClip clip)
        {
            bool rootMotion = animator.applyRootMotion;
            animator.applyRootMotion = true;
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            output.SetSourcePlayable(playable);
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            playable.SetTime(0);
            graph.Evaluate();
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            int steps = Mathf.Max(1, Mathf.RoundToInt(clip.length * FRAME_RATE) - 1);
            for (int i = 0; i < steps; i++)
                graph.Evaluate(1f / FRAME_RATE);
            Vector3 moved = root.position;
            playable.Destroy();
            animator.applyRootMotion = rootMotion;
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return new Vector2(moved.x, moved.z).magnitude / (steps / FRAME_RATE);
        }

        /// <summary>
        /// Index buffer of the first generated Mesh LOD with at most <see cref="MAX_TRIANGLES"/>
        /// triangles, generated on a copy of the source. The full mesh if generation gives nothing.
        /// </summary>
        private static int[] CutLod(Mesh source, out int lod, out int lodCount)
        {
            Mesh copy = Object.Instantiate(source);
            try
            {
                MeshLodUtility.GenerateMeshLods(copy, 0, MAX_LODS);
                lodCount = copy.lodCount;
                lod = 0;
                for (int i = 0; i < lodCount; i++)
                {
                    lod = i;
                    if (copy.GetLod(0, i).indexCount / 3 <= MAX_TRIANGLES)
                        break;
                }

                MeshLodRange range = copy.GetLod(0, lod);
                var result = new int[range.indexCount];
                using Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(copy);
                Mesh.MeshData mesh = data[0];
                if (mesh.indexFormat == IndexFormat.UInt16)
                {
                    NativeArray<ushort> all = mesh.GetIndexData<ushort>();
                    for (int k = 0; k < result.Length; k++)
                        result[k] = all[(int)range.indexStart + k];
                }
                else
                {
                    NativeArray<int> all = mesh.GetIndexData<int>();
                    for (int k = 0; k < result.Length; k++)
                        result[k] = all[(int)range.indexStart + k];
                }

                return result;
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        private static Vector3 ToVector(Color c) => new(c.r, c.g, c.b);

        private static AnimationClip LoadFbxClip(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            return null;
        }

        /// <summary>Frames of a clip played as a loop: the last frame is the first again, so it is not stored.</summary>
        private static int LoopFrames(AnimationClip clip) => Mathf.Max(1, Mathf.RoundToInt(clip.length * FRAME_RATE));

        private static Mesh BuildMesh(string name, Vector3[] positions, Vector3[] normals, Vector2[] uv, Vector2[] ids, int[] indices)
        {
            var mesh = new Mesh { name = name, indexFormat = positions.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.uv2 = ids;
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static T SaveOrReplace<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }

            EditorUtility.CopySerialized(asset, existing);
            existing.name = asset.name;
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void SetNotReadable(Object asset)
        {
            var so = new SerializedObject(asset);
            SerializedProperty readable = so.FindProperty("m_IsReadable");
            if (readable == null)
                return;
            readable.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <param name="textured">The crowd's material (the albedo); false: the plain one for acid.</param>
        private static Material SaveMaterial(Source src, string path, Texture2D vat, int frames, bool textured)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(SHADER_NAME);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            if (src.SourceMaterialPath != null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(src.SourceMaterialPath);
                if (source != null)
                {
                    if (source.HasProperty("_Color"))
                        material.SetColor("_Color", source.GetColor("_Color"));
                    if (source.HasProperty("_EmissiveTex"))
                        material.SetTexture("_EmissionMap", source.GetTexture("_EmissiveTex"));
                }
            }

            if (src.AlbedoPath != null)
            {
                material.SetColor("_Color", textured ? src.AlbedoColor : Color.white);
                material.SetTexture("_MainTex", textured ? AssetDatabase.LoadAssetAtPath<Texture2D>(src.AlbedoPath) : null);
                material.SetColor("_EmissionColor", Color.black);
            }

            material.SetTexture("_HordeVat", vat);
            material.SetFloat("_HordeVatFrames", frames);
            material.SetFloat("_HordeScale", 1f);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
