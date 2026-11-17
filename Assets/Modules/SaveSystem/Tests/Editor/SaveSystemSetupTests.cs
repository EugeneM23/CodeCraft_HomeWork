using NUnit.Framework;
using Saving.Editor;
using Saving.Objects;
using Saving.UI;
using Saving.UI.Editor;
using UnityEditor;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>Drop-in setup in the editor: the settings asset finds itself, the module's prefabs are there and wired.</summary>
    public class SaveSystemSetupTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        [Test]
        public void AddComponent_GetsTheProjectSettings()
        {
            var system = new GameObject("SaveSystem").AddComponent<SaveLoadSystem>();

            Assert.IsTrue(system.HasSettings);
            Assert.AreSame(SaveSystemSetup.FindOrCreateSettings(),
                new SerializedObject(system).FindProperty("_settings").objectReferenceValue);
        }

        [Test]
        public void AssignSettings_SceneObjectsOff_LeavesItEmpty()
        {
            var system = new GameObject("SaveSystem").AddComponent<SaveLoadSystem>();
            var serialized = new SerializedObject(system);
            serialized.FindProperty("_saveSceneObjects").boolValue = false;
            serialized.FindProperty("_settings").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(SaveSystemSetup.AssignSettings(system));
            Assert.IsFalse(system.HasSettings);
        }

        [Test]
        public void Prefab_SaveSystem_LoadsOnStartWithoutSettings()
        {
            GameObject prefab = SaveSystemPrefabs.SystemPrefab;
            Assert.IsNotNull(prefab, $"no {SaveSystemPrefabs.SYSTEM_PREFAB} in {SaveSystemPrefabs.PrefabsFolder}");

            var serialized = new SerializedObject(prefab.GetComponent<SaveLoadSystem>());
            Assert.IsTrue(serialized.FindProperty("_loadOnStart").boolValue);
            Assert.IsTrue(serialized.FindProperty("_keepAcrossScenes").boolValue);
            Assert.IsNull(serialized.FindProperty("_settings").objectReferenceValue, "each project sets its own");
        }

        [Test]
        public void Prefab_SaveSlotsMenu_PartsWired()
        {
            GameObject prefab = SaveSystemPrefabs.MenuPrefab;
            Assert.IsNotNull(prefab, $"no {SaveSystemPrefabs.MENU_PREFAB} in {SaveSystemPrefabs.PrefabsFolder}");

            var serialized = new SerializedObject(prefab.GetComponent<SaveSlotsMenu>());
            foreach (string part in new[] { "_panel", "_title", "_list", "_slotTemplate", "_closeButton", "_confirm" })
                Assert.IsNotNull(serialized.FindProperty(part).objectReferenceValue, part);
        }

        [Test]
        public void NewSettingsPath_FoundInPlayByScenesWithoutASystem()
        {
            Assert.IsTrue(SaveSystemSetup.IsRuntimeSettingsPath(SaveSystemSetup.NEW_SETTINGS_PATH));
        }

        [Test]
        public void IsRuntimeSettingsPath_OutsideResourcesNestedOrRenamed_False()
        {
            Assert.IsFalse(SaveSystemSetup.IsRuntimeSettingsPath("Assets/Settings/SaveSystemSettings.asset"));
            Assert.IsFalse(SaveSystemSetup.IsRuntimeSettingsPath("Assets/Settings/Resources/Saves/SaveSystemSettings.asset"));
            Assert.IsFalse(SaveSystemSetup.IsRuntimeSettingsPath("Assets/Settings/Resources/MySaveSettings.asset"));
            Assert.IsFalse(SaveSystemSetup.IsRuntimeSettingsPath(null));
        }

        [Test]
        public void NewSettings_CreateSystemWhenMissingOn()
        {
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();

            Assert.IsTrue(settings.CreateSystemWhenMissing);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void AddSaveable_SettingsExist_NoNewAsset()
        {
            int before = AssetDatabase.FindAssets("t:" + nameof(SaveSystemSettings)).Length;

            new GameObject("Box").AddComponent<Saveable>();

            Assert.AreEqual(before, AssetDatabase.FindAssets("t:" + nameof(SaveSystemSettings)).Length);
        }

        [Test]
        public void Inspector_CreateSystemWhenMissingField_Found()
        {
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();

            Assert.IsNotNull(new SerializedObject(settings).FindProperty("_createSystemWhenMissing"),
                "SaveSystemSettingsEditor draws the flag by this name");
            Object.DestroyImmediate(settings);
        }
    }
}
