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
    /// Bakes the Regax (SK_Rapax) for the horde: a low-poly copy of the skin (Unity Mesh LOD
    /// generation on a copy; the FBX import settings are not touched), its vertex animation texture
    /// (Run, RightClawsAttack, IdleBreathe sampled at 30 fps with a PlayableGraph and
    /// <see cref="SkinnedMeshRenderer.BakeMesh(Mesh, bool)"/>), the corpse mesh (last frame of Death),
    /// the material and <see cref="HordeArtConfig"/>, all in <c>Assets/Game/Horde/Art/</c>.
    /// Re-baking overwrites the assets in place (their GUIDs stay).
    /// </summary>
    public static class HordeVatBaker
    {
        public const string OUTPUT_FOLDER = "Assets/Game/Horde/Art";
        public const string CONFIG_PATH = OUTPUT_FOLDER + "/RegaxHordeArtConfig.asset";

        private const string SOURCE_PATH = "Assets/3rdParty/Rapax/FBX Files/SK_Rapax.fbx";
        private const string CLIP_PREFIX = "Assets/3rdParty/Rapax/FBX Files/Rapax@";
        private const string SOURCE_MATERIAL_PATH = "Assets/3rdParty/Rapax/Materials/M_Rapax 1.mat";
        private const string SHADER_NAME = "Game/HordeVAT";
        private const float FRAME_RATE = 30f;
        private const int MAX_TRIANGLES = 2000;
        private const int MAX_LODS = 8;

        [MenuItem("Tools/Horde/Bake Regax VAT")]
        private static void BakeFromMenu() => Debug.Log(Bake());

        /// <summary>Bakes everything; returns a short report.</summary>
        public static string Bake()
        {
            var report = new StringBuilder();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE_PATH);
            if (prefab == null)
                return "HordeVatBaker: no " + SOURCE_PATH;

            Scene scene = EditorSceneManager.NewPreviewScene();
            var graph = PlayableGraph.Create("HordeVatBaker");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                var animator = instance.GetComponentInChildren<Animator>();
                animator.applyRootMotion = false;
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
                AnimationClip run = LoadClip("Run");
                AnimationClip attack = LoadClip("RightClawsAttack");
                AnimationClip idle = LoadClip("IdleBreathe");
                AnimationClip death = LoadClip("Death");
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

                // World space of the baked skin (BakeMesh with scale gives the renderer's space minus its TRS).
                Vector3 ToModel(Vector3 p) => skin.transform.rotation * p + skin.transform.position;
                Vector3 ToModelDir(Vector3 n) => (skin.transform.rotation * n).normalized;

                int row = 0;
                void BakeClip(AnimationClip clip, int count)
                {
                    for (int f = 0; f < count; f++, row++)
                    {
                        Sample(clip, f / FRAME_RATE);
                        for (int v = 0; v < vertexCount; v++)
                        {
                            Vector3 p = ToModel(vertices[used[v]]);
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
                                firstPositions[v] = ToModel(vertices[used[v]]);
                                firstNormals[v] = ToModelDir(normals[used[v]]);
                            }
                        }
                    }
                }

                BakeClip(run, runFrames);
                Vector3 runDrift = ToVector(pixels[(runFrames - 1) * vertexCount]) - ToVector(pixels[0]);
                BakeClip(attack, attackFrames);
                BakeClip(idle, idleFrames);

                // Corpse: the last frame of Death.
                Sample(death, death.length);
                var corpsePositions = new Vector3[vertexCount];
                var corpseNormals = new Vector3[vertexCount];
                for (int v = 0; v < vertexCount; v++)
                {
                    corpsePositions[v] = ToModel(vertices[used[v]]);
                    corpseNormals[v] = ToModelDir(normals[used[v]]);
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

                Mesh crowd = BuildMesh("SM_HordeRegax", firstPositions, firstNormals, meshUv, ids, indices);
                crowd.bounds = bounds;
                crowd = SaveOrReplace(crowd, OUTPUT_FOLDER + "/SM_HordeRegax.asset");

                Mesh corpse = BuildMesh("SM_HordeRegax_Corpse", corpsePositions, corpseNormals, meshUv, ids, indices);
                corpse = SaveOrReplace(corpse, OUTPUT_FOLDER + "/SM_HordeRegax_Corpse.asset");

                var vat = new Texture2D(vertexCount, frames * 2, TextureFormat.RGBAHalf, false, true)
                {
                    name = "T_HordeRegax_VAT",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                vat.SetPixels(pixels);
                vat.Apply(false, false);
                vat = SaveOrReplace(vat, OUTPUT_FOLDER + "/T_HordeRegax_VAT.asset");
                SetNotReadable(crowd);
                SetNotReadable(corpse);
                SetNotReadable(vat);

                Material material = SaveMaterial(vat, frames);

                var config = AssetDatabase.LoadAssetAtPath<HordeArtConfig>(CONFIG_PATH);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<HordeArtConfig>();
                    AssetDatabase.CreateAsset(config, CONFIG_PATH);
                }

                config.mesh = crowd;
                config.corpseMesh = corpse;
                config.material = material;
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
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                report.Append("saved to ").Append(OUTPUT_FOLDER);
                return report.ToString();
            }
            finally
            {
                if (graph.IsValid())
                    graph.Destroy();
                EditorSceneManager.ClosePreviewScene(scene);
            }
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

        private static AnimationClip LoadClip(string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CLIP_PREFIX + name + ".fbx"))
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

        private static Material SaveMaterial(Texture2D vat, int frames)
        {
            string path = OUTPUT_FOLDER + "/MT_HordeRegax.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(SHADER_NAME);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            var source = AssetDatabase.LoadAssetAtPath<Material>(SOURCE_MATERIAL_PATH);
            if (source != null)
            {
                if (source.HasProperty("_Color"))
                    material.SetColor("_Color", source.GetColor("_Color"));
                if (source.HasProperty("_EmissiveTex"))
                    material.SetTexture("_EmissionMap", source.GetTexture("_EmissiveTex"));
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
