using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Saving.Editor;
using Saving.Objects;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>Prefab ids baked into assets, the catalogs of SaveSystemSettings and the scene check.</summary>
    public class SaveCatalogTests
    {
        // Next to the tests; a module installed as a package is read-only, so then in Assets
        private static readonly string PARENT = TestScenes.MODULE.StartsWith("Packages/")
            ? "Assets"
            : TestScenes.MODULE + "/Tests/Editor";
        private static readonly string FOLDER = PARENT + "/TempAssets";

        private TempScene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = new TempScene();
            AssetDatabase.CreateFolder(PARENT, "TempAssets");
        }

        [TearDown]
        public void TearDown()
        {
            _scene.Dispose();
            AssetDatabase.DeleteAsset(FOLDER);
            // The project's settings may have picked up the temporary prefabs: put their catalogs back in order
            foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                SaveCatalogBuilder.Rebuild(settings);
        }

        private static string MakePrefab(string name, bool withSaveable = true, bool withNested = false)
        {
            var root = new GameObject(name);
            if (withSaveable)
                root.AddComponent<Saveable>().SetId("33333333333333333333333333333333");

            if (withNested)
            {
                var child = new GameObject("Child");
                child.transform.SetParent(root.transform, false);
                child.AddComponent<Saveable>().SetId("44444444444444444444444444444444");
            }

            string path = $"{FOLDER}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }

        private static Saveable RootOf(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Saveable>();

        [Test]
        public void Bake_Prefab_PrefabIdIsItsGuidAndNoIds()
        {
            string path = MakePrefab("Thing", withNested: true);

            SaveCatalogBuilder.BakePrefabIds(new[] { path });

            Saveable root = RootOf(path);
            Saveable child = root.transform.Find("Child").GetComponent<Saveable>();
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(path), root.PrefabId);
            Assert.AreEqual(string.Empty, root.Id);
            Assert.AreEqual(string.Empty, child.Id);
            Assert.AreEqual(string.Empty, child.PrefabId);
        }

        [Test]
        public void Bake_Variant_HasItsOwnPrefabId()
        {
            string basePath = MakePrefab("Base");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath));
            string variantPath = $"{FOLDER}/Variant.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            Object.DestroyImmediate(instance);

            SaveCatalogBuilder.BakePrefabIds(new[] { basePath, variantPath });

            Assert.AreEqual(AssetDatabase.AssetPathToGUID(variantPath), RootOf(variantPath).PrefabId);
            Assert.AreNotEqual(RootOf(basePath).PrefabId, RootOf(variantPath).PrefabId);
        }

        [Test]
        public void Bake_PrefabWithMissingScript_LeftAsItIsWithAnError()
        {
            // Unity refuses to save such a prefab: baking it in memory only would make scene instances load as copies
            string name = "Broken" + System.Guid.NewGuid().ToString("N");
            var root = new GameObject(name);
            root.AddComponent<Saveable>().SetId("33333333333333333333333333333333");
            var probe = root.AddComponent<MembersProbe>();
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(MonoScript.FromMonoBehaviour(probe), out string scriptGuid, out long _));
            string path = $"{FOLDER}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            System.IO.File.WriteAllText(path, System.IO.File.ReadAllText(path).Replace(scriptGuid, "0000000000000000000000000000dead"));
            LogAssert.ignoreFailingMessages = true; // Unity's own complaints about the missing script while importing
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            LogAssert.ignoreFailingMessages = false;

            LogAssert.Expect(LogType.Error, new Regex(name + ".*has a missing script"));
            int changed = SaveCatalogBuilder.BakePrefabIds(new[] { path });

            Assert.AreEqual(0, changed);
            Assert.AreEqual("33333333333333333333333333333333", RootOf(path).Id, "not changed in memory either");
            Assert.AreEqual(string.Empty, RootOf(path).PrefabId);
        }

        [Test]
        public void Ids_PrefabAsset_KeepsNoId()
        {
            string path = MakePrefab("Asset");

            SaveableIdAssigner.Assign(RootOf(path));

            Assert.AreEqual(string.Empty, RootOf(path).Id);
        }

        [Test]
        public void Rebuild_FindsSaveablePrefabsAndScenes()
        {
            string withSaveable = MakePrefab("Saved");
            string plain = MakePrefab("Plain", withSaveable: false);
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();

            SaveCatalogBuilder.Rebuild(settings);

            Assert.IsTrue(settings.TryGetPrefab(AssetDatabase.AssetPathToGUID(withSaveable), out GameObject prefab));
            Assert.AreEqual(withSaveable, AssetDatabase.GetAssetPath(prefab));
            Assert.IsFalse(settings.TryGetPrefab(AssetDatabase.AssetPathToGUID(plain), out _));
            string testScene = TestScenes.SCENE;
            Assert.IsTrue(settings.TryGetSceneGuid(testScene, out string guid));
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(testScene), guid);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Rebuild_SkipsTestRunnerScenes()
        {
            // The Test Framework saves a temporary InitTestScene*.unity for every PlayMode run and deletes it afterwards
            string testScene = TestScenes.SCENE;
            string runnerScene = $"{FOLDER}/InitTestScene0123.unity";
            Assert.IsTrue(AssetDatabase.CopyAsset(testScene, runnerScene));
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();

            SaveCatalogBuilder.Rebuild(settings);

            Assert.IsFalse(settings.TryGetSceneGuid(runnerScene, out _));
            Assert.IsTrue(settings.TryGetSceneGuid(testScene, out _));
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Validate_StaleCatalog_Reported()
        {
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            SaveCatalogBuilder.Rebuild(settings);
            Assert.IsEmpty(SaveValidator.ValidateCatalogs(settings));

            MakePrefab("Late");

            Assert.IsTrue(SaveValidator.ValidateCatalogs(settings).Any(problem => problem.Contains("Late")));
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Validate_EmptyAndRepeatedIds_Reported()
        {
            var empty = new GameObject("Empty").AddComponent<Saveable>();
            empty.SetId(string.Empty);
            new GameObject("A").AddComponent<Saveable>().SetId("55555555555555555555555555555555");
            new GameObject("B").AddComponent<Saveable>().SetId("55555555555555555555555555555555");

            var problems = SaveValidator.ValidateScene(_scene.Scene);

            Assert.AreEqual(2, problems.Count);
            Assert.IsTrue(problems.Any(problem => problem.Contains("'Empty' has no id")));
            Assert.IsTrue(problems.Any(problem => problem.Contains("'B' repeats the id of 'A'")));
        }

        [Test]
        public void Rebuild_AssetTableFromFolders()
        {
            string art = TestScenes.ROOT + "/Art";
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            settings.SetAssetFolders(new[] { art });

            SaveCatalogBuilder.Rebuild(settings);

            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(art + "/SaveTestIcon.png");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "/SaveTestIcon.png");
            Assert.IsTrue(settings.TryGetAssetKey(icon, out string iconKey));
            Assert.IsTrue(settings.TryGetAssetKey(texture, out string textureKey));
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(art + "/SaveTestIcon.png"), textureKey);
            Assert.IsTrue(settings.TryGetAsset(iconKey, out Object found));
            Assert.AreSame(icon, found);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Validate_StaleAssetTable_Reported()
        {
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            settings.SetAssetFolders(new[] { FOLDER });
            SaveCatalogBuilder.Rebuild(settings);
            Assert.IsTrue(AssetDatabase.CopyAsset(TestScenes.ICON, FOLDER + "/LateIcon.png"));

            Assert.IsTrue(SaveValidator.ValidateCatalogs(settings).Exists(problem => problem.Contains("LateIcon")));
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Postprocess_LargeBatchOfUnrelatedPaths_Fast()
        {
            // A domain reload hands the postprocessor thousands of paths: the cost must not grow with a project search per path
            string[] paths = Enumerable.Range(0, 1000).Select(i => $"{FOLDER}/Unrelated{i}.txt").ToArray();
            System.Reflection.MethodInfo postprocess = typeof(SaveCatalogBuilder).Assembly
                .GetType("Saving.Editor.SaveCatalogPostprocessor")
                .GetMethod("OnPostprocessAllAssets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var empty = new string[0];

            var watch = System.Diagnostics.Stopwatch.StartNew();
            postprocess.Invoke(null, new object[] { paths, empty, empty, empty });

            Assert.Less(watch.Elapsed.TotalSeconds, 2.0);
        }
    }
}
