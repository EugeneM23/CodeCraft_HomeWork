using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Saving.Tests
{
    /// <summary>The module's test scene and prefab: paths, fixed ids, loading and waiting.</summary>
    public static class TestScenes
    {
        /// <summary>The module's Tests/Runtime folder, wherever the module lies: the module moves with its tests.</summary>
        public static readonly string ROOT = FindRoot();

        /// <summary>The module folder (the parent of Tests).</summary>
        public static readonly string MODULE = ROOT.Substring(0, Math.Max(0, ROOT.LastIndexOf("/Tests/", StringComparison.Ordinal)));

        public static readonly string SCENE = ROOT + "/Scenes/SaveTestScene.unity";
        public static readonly string PREFAB = ROOT + "/Prefabs/SaveTestSpawn.prefab";

        /// <summary>A scene whose save system is set to Whole Scene; built by WholeSceneTestAssets (Tools → Save System → Tests).</summary>
        public static readonly string WHOLE_SCENE = ROOT + "/Scenes/SaveTestWholeScene.unity";

        /// <summary>A prefab without a Saveable that the whole-scene scene spawns.</summary>
        public static readonly string PLAIN_PREFAB = ROOT + "/Prefabs/SaveTestPlainSpawn.prefab";

        /// <summary>A one-shot effect: a particle system and nothing else, as a game spawns an explosion.</summary>
        public static readonly string PLAIN_EFFECT = ROOT + "/Prefabs/SaveTestPlainEffect.prefab";
        public const string MARKED = "5a0e0000000000000000000000000005";
        public const string BOX = "5a0e0000000000000000000000000001";
        public const string HIDDEN = "5a0e0000000000000000000000000002";
        public const string DOOMED = "5a0e0000000000000000000000000003";
        public static readonly string ANIMATOR = ROOT + "/Animation/SaveTestAnimator.controller";

        /// <summary>Loads the test scene (editor only: it is not in Build Settings) and waits until it has started.</summary>
        public static IEnumerator Load() => Load(SCENE);

        public static IEnumerator Load(string path)
        {
#if UNITY_EDITOR
            Scene scene = EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitFor(() => scene.isLoaded);
            yield return null; // Start of the scene's objects
#else
            Assert.Ignore("The save system test scenes load only in the editor.");
            yield break;
#endif
        }

        public static GameObject Prefab =>
#if UNITY_EDITOR
            AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
#else
            null;
#endif

        public static GameObject PlainPrefab =>
#if UNITY_EDITOR
            AssetDatabase.LoadAssetAtPath<GameObject>(PLAIN_PREFAB);
#else
            null;
#endif

        public static GameObject EffectPrefab =>
#if UNITY_EDITOR
            AssetDatabase.LoadAssetAtPath<GameObject>(PLAIN_EFFECT);
#else
            null;
#endif

        public static string GuidOf(string path) =>
#if UNITY_EDITOR
            AssetDatabase.AssetPathToGUID(path);
#else
            string.Empty;
#endif

        public static GameObject Find(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(root => root.name == name);

        /// <summary>Settings with the test prefab and the test scene in their catalogs.</summary>
        public static SaveSystemSettings CreateSettings()
        {
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            settings.SetCatalogs(
                new[] { new SaveSystemSettings.PrefabEntry { Id = GuidOf(PREFAB), Prefab = Prefab } },
                new[]
                {
                    new SaveSystemSettings.SceneEntry { Guid = GuidOf(SCENE), Path = SCENE },
                    new SaveSystemSettings.SceneEntry { Guid = GuidOf(WHOLE_SCENE), Path = WHOLE_SCENE }
                });
            settings.SetAssetCatalog(IconEntries());
            return settings;
        }

        public static readonly string ICON = ROOT + "/Art/SaveTestIcon.png";
        public const string LINKER = "5a0e0000000000000000000000000004";

        public static Sprite Icon =>
#if UNITY_EDITOR
            AssetDatabase.LoadAssetAtPath<Sprite>(ICON);
#else
            null;
#endif

        /// <summary>The assets of a file as entries of the asset table, keyed the way the editor keys them.</summary>
        public static SaveSystemSettings.AssetEntry[] EntriesOf(string path)
        {
#if UNITY_EDITOR
            var entries = new System.Collections.Generic.List<SaveSystemSettings.AssetEntry>();
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                    entries.Add(new SaveSystemSettings.AssetEntry
                    {
                        Key = SaveSystemSettings.AssetKey(guid, localId, AssetDatabase.IsMainAsset(asset)),
                        Asset = asset
                    });
            }

            return entries.ToArray();
#else
            return new SaveSystemSettings.AssetEntry[0];
#endif
        }

        public static SaveSystemSettings.AssetEntry[] IconEntries() => EntriesOf(ICON);

        public static readonly string TONE = ROOT + "/Audio/SaveTestTone.wav";

        public static AudioClip Tone =>
#if UNITY_EDITOR
            AssetDatabase.LoadAssetAtPath<AudioClip>(TONE);
#else
            null;
#endif

        private static string FindRoot()
        {
#if UNITY_EDITOR
            const string ASSEMBLY = "SaveSystem.Tests.Runtime";
            foreach (string guid in AssetDatabase.FindAssets(ASSEMBLY + " t:AssemblyDefinitionAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == ASSEMBLY)
                    return path.Substring(0, path.LastIndexOf('/'));
            }
#endif
            return string.Empty;
        }

        public static IEnumerator WaitFor(Func<bool> condition, int frames = 300)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                if (condition())
                    yield break;

                yield return null;
            }

            Assert.Fail($"The condition did not come true in {frames} frames.");
        }
    }
}
