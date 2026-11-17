using Saving.Editor;
using Saving.Objects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saving.Tests
{
    /// <summary>
    /// Builds the scene and the prefab of the whole-scene tests by code, so nobody edits them by hand: a save system
    /// set to Whole Scene and plain objects around it — moved, left alone, destroyed, switched off, static, with a
    /// script, with a body, with a child, one that makes an object in Start, one switched off in the scene file, one
    /// set up by hand with a Saveable.
    /// </summary>
    public static class WholeSceneTestAssets
    {
        [MenuItem("Tools/Save System/Tests/Rebuild Whole Scene Test Assets")]
        public static void Rebuild()
        {
            Scene previous = SceneManager.GetActiveScene();
            GameObject prefab = BuildPrefab();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            Make("Plain", 1f);
            Make("Still", 2f);
            Make("Doomed", 3f);
            Make("Sleeper", 4f);
            Make("Wall", 5f).isStatic = true;
            Make("Counter", 6f).AddComponent<SaveTestCounter>();
            Make("Probe", 7f).AddComponent<MembersProbe>();
            Make("Body", 8f).AddComponent<Rigidbody>().useGravity = false;
            var child = new GameObject("Child");
            child.transform.SetParent(Make("Holder", 9f).transform, false);
            Make("Giver", 10f).AddComponent<SaveTestStartSpawner>().Prefab = prefab;
            // Switched off in the scene file, like a pause menu: its first Awake comes when the game turns it on
            Make("Hidden", 12f).SetActive(false);
            // Something in the scene points at the effect prefab, as an enemy points at its explosion: that is how the
            // scene comes to know the prefab
            Make("Exploder", 13f).AddComponent<MembersProbe>().Target = BuildEffectPrefab().transform;

            // An object set up by hand keeps its own list: its script, not its place
            GameObject marked = Make("Marked", 11f);
            marked.AddComponent<SaveTestCounter>();
            var inner = new GameObject("Inner");
            inner.transform.SetParent(marked.transform, false);
            inner.AddComponent<MembersProbe>(); // not in the list: the scope of the scene takes it
            marked.AddComponent<MembersProbe>(); // in the list with one member picked: the scope adds the rest
            var saveable = marked.AddComponent<Saveable>();
            saveable.UseExplicitList(new[] { "SaveTestCounter" });
            saveable.SetMembers("MembersProbe", new[] { "_timer" });
            saveable.SetId(TestScenes.MARKED);

            var system = new GameObject("SaveSystem").AddComponent<SaveLoadSystem>();
            var serialized = new SerializedObject(system);
            // Never the working one in tests: it must not load or write the project's real slots
            serialized.FindProperty("_loadOnStart").boolValue = false;
            serialized.FindProperty("_saveOnQuit").boolValue = false;
            serialized.FindProperty("_saveOnPause").boolValue = false;
            serialized.FindProperty("_autoSavePeriod").floatValue = 0f;
            serialized.FindProperty("_hotkeys").boolValue = false;
            serialized.FindProperty("_scope").enumValueIndex = (int)SaveScope.WholeScene;
            serialized.FindProperty("_settings").objectReferenceValue = null; // the tests bring their own
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Saved twice: the prefab list comes from what the saved scene depends on
            EditorSceneManager.SaveScene(scene, TestScenes.WHOLE_SCENE);
            SaveSceneBaker.BakeScene(scene);
            EditorSceneManager.SaveScene(scene, TestScenes.WHOLE_SCENE);
            Debug.Log($"Whole-scene test assets rebuilt: {system.SceneObjects.Count} objects, {system.ScenePrefabs.Count} prefab(s) listed.");

            if (previous.IsValid() && !string.IsNullOrEmpty(previous.path))
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.SaveAssets();
        }

        // A script on the root and one on a child, no Saveable: what a game spawns without thinking about saves
        private static GameObject BuildPrefab()
        {
            var root = new GameObject("SaveTestPlainSpawn");
            root.AddComponent<SaveTestCounter>();
            var part = new GameObject("Part");
            part.transform.SetParent(root.transform, false);
            part.AddComponent<SaveTestCounter>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TestScenes.PLAIN_PREFAB);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // No script, no body: a short burst of particles that does not loop
        private static GameObject BuildEffectPrefab()
        {
            var root = new GameObject("SaveTestPlainEffect");
            ParticleSystem.MainModule main = root.AddComponent<ParticleSystem>().main;
            main.loop = false;
            main.duration = 1.5f;
            main.startLifetime = 0.5f;
            main.playOnAwake = true;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TestScenes.PLAIN_EFFECT);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject Make(string name, float x)
        {
            var made = new GameObject(name);
            made.transform.position = new Vector3(x, 0f, 0f);
            return made;
        }
    }
}
