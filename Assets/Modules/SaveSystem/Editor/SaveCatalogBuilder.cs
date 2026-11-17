using System;
using System.Collections.Generic;
using System.Linq;
using Saving.Objects;
using UnityEditor;
using UnityEngine;

namespace Saving.Editor
{
    /// <summary>
    /// Keeps prefab ids and the catalogs of SaveSystemSettings in order: a prefab with a Saveable on its root has its
    /// asset GUID as prefab id (a variant has its own), ids inside prefab assets stay empty; the catalogs list those
    /// prefabs and every scene in Assets (and in the module, when it is installed as a package). Runs after imports,
    /// before builds and from the menu.
    /// </summary>
    public static class SaveCatalogBuilder
    {
        private const string SAVEABLE_TAIL = "/Runtime/Objects/Saveable.cs";

        private static string[] _roots;

        /// <summary>Assets, and the module itself when it is installed as a package: its demo and test scenes and prefabs.</summary>
        private static string[] Root
        {
            get
            {
                if (_roots != null)
                    return _roots;

                string script = SaveableScriptPath();
                _roots = script.StartsWith("Packages/", StringComparison.Ordinal) && script.EndsWith(SAVEABLE_TAIL, StringComparison.Ordinal)
                    ? new[] { "Assets", script.Substring(0, script.Length - SAVEABLE_TAIL.Length) }
                    : new[] { "Assets" };
                return _roots;
            }
        }

        public static int BakePrefabIds(IEnumerable<string> prefabPaths = null)
        {
            int changed = 0;
            foreach (string path in prefabPaths ?? PrefabPathsWithSaveable())
            {
                // A package is read-only: its prefabs come baked
                if (path.StartsWith("Packages/", StringComparison.Ordinal))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                string guid = AssetDatabase.AssetPathToGUID(path);
                Saveable[] saveables = prefab.GetComponentsInChildren<Saveable>(true);
                if (!saveables.Any(saveable => NeedsBaking(saveable, prefab, guid)))
                    continue;

                // Unity refuses to save a prefab with a missing script: changing it in memory only would leave the
                // file unbaked while the editor shows baked ids, and scene instances would load as spawned copies
                if (HasMissingScript(prefab))
                {
                    if (MissingScriptWarned.Add(path))
                        Debug.LogError($"The prefab '{path}' has a missing script, so Unity cannot save it and the ids of " +
                                       "its Saveable are not set up: its objects will not be saved and restored correctly. " +
                                       "Remove the missing script and run Tools → Save System → Rebuild Catalogs.", prefab);
                    continue;
                }

                foreach (Saveable saveable in saveables)
                {
                    var serialized = new SerializedObject(saveable);
                    bool isRoot = saveable.transform == prefab.transform;
                    bool changedHere = SetString(serialized, "_id", string.Empty)
                                       | SetString(serialized, "_prefabId", isRoot ? guid : string.Empty);
                    if (changedHere)
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SavePrefabAsset(prefab, out bool saved);
                if (saved)
                    changed++;
                else
                    Debug.LogError($"The prefab '{path}' could not be saved; the ids of its Saveable are not set up.", prefab);
            }

            return changed;
        }

        private static readonly HashSet<string> MissingScriptWarned = new();

        private static bool NeedsBaking(Saveable saveable, GameObject prefab, string guid) =>
            !string.IsNullOrEmpty(saveable.Id)
            || (saveable.PrefabId ?? string.Empty) != (saveable.transform == prefab.transform ? guid : string.Empty);

        private static bool HasMissingScript(GameObject prefab) =>
            prefab.GetComponentsInChildren<Transform>(true)
                .Any(node => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0);

        /// <summary>Rebuilds the catalogs; the asset table only when asked (loading every asset of the folders costs time).</summary>
        public static void Rebuild(SaveSystemSettings settings, bool includeAssets = true)
        {
            List<SaveSystemSettings.PrefabEntry> prefabs = BuildPrefabEntries();
            List<SaveSystemSettings.SceneEntry> scenes = BuildSceneEntries();
            List<SaveSystemSettings.AssetEntry> assets = includeAssets ? BuildAssetEntries(settings) : null;
            List<string> thirdParty = BuildThirdPartyAssemblies();
            bool catalogsSame = Same(settings, prefabs, scenes);
            bool assetsSame = assets == null || SameAssets(settings, assets);
            bool thirdPartySame = settings.ThirdPartyAssemblies.SequenceEqual(thirdParty);
            // The asset on disk may be up to date while this domain has not heard of the list yet
            SaveMembers.SetThirdPartyAssemblies(thirdParty);
            if (catalogsSame && assetsSame && thirdPartySame)
                return;

            if (!catalogsSame)
                settings.SetCatalogs(prefabs, scenes);
            if (!assetsSame)
                settings.SetAssetCatalog(assets);
            if (!thirdPartySame)
                settings.SetThirdPartyAssemblies(thirdParty);

            if (!EditorUtility.IsPersistent(settings))
                return;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        /// <summary>
        /// Assemblies of code the game did not write: compiled from a Plugins folder of Assets or from a package that
        /// came from a registry or git, and the managed dlls plugins bring. Unity's own are known without a list; the
        /// module's are left out so its demo and tests behave the same wherever the module is put.
        /// </summary>
        public static List<string> BuildThirdPartyAssemblies()
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (UnityEditor.Compilation.Assembly assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies())
            {
                if (!IsUnityOrModule(assembly.name) && assembly.sourceFiles.Length > 0 && assembly.sourceFiles.All(IsThirdPartySource))
                    names.Add(assembly.name);
            }

            foreach (string path in UnityEditor.Compilation.CompilationPipeline.GetPrecompiledAssemblyPaths(
                         UnityEditor.Compilation.CompilationPipeline.PrecompiledAssemblySources.UserAssembly))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!IsUnityOrModule(name))
                    names.Add(name);
            }

            return names.ToList();
        }

        private static bool IsUnityOrModule(string assembly) =>
            assembly.StartsWith("Unity.", StringComparison.Ordinal) || assembly.StartsWith("UnityEngine", StringComparison.Ordinal)
            || assembly.StartsWith("UnityEditor", StringComparison.Ordinal) || assembly == "SaveSystem"
            || assembly.StartsWith("SaveSystem.", StringComparison.Ordinal) || assembly == "nunit.framework"
            || assembly == "Newtonsoft.Json";

        private static readonly Dictionary<string, bool> PackageIsThirdParty = new(StringComparer.Ordinal);

        private static bool IsThirdPartySource(string path)
        {
            path = path.Replace('\\', '/');
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
                return path.Contains("/Plugins/");

            if (!path.StartsWith("Packages/", StringComparison.Ordinal))
                return false;

            // Packages/<name>/...: one lookup per package
            int end = path.IndexOf('/', "Packages/".Length);
            string package = end < 0 ? path : path.Substring(0, end);
            if (!PackageIsThirdParty.TryGetValue(package, out bool thirdParty))
            {
                UnityEditor.PackageManager.PackageInfo info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                // A package of the project itself (embedded, a local folder) is the game's own code
                thirdParty = info != null && info.source != UnityEditor.PackageManager.PackageSource.Embedded
                                          && info.source != UnityEditor.PackageManager.PackageSource.Local;
                PackageIsThirdParty[package] = thirdParty;
            }

            return thirdParty;
        }

        public static List<SaveSystemSettings.PrefabEntry> BuildPrefabEntries()
        {
            var entries = new List<SaveSystemSettings.PrefabEntry>();
            foreach (string path in PrefabPathsWithSaveable())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.TryGetComponent(out Saveable _))
                    entries.Add(new SaveSystemSettings.PrefabEntry { Id = AssetDatabase.AssetPathToGUID(path), Prefab = prefab });
            }

            return entries.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToList();
        }

        public static List<SaveSystemSettings.SceneEntry> BuildSceneEntries() =>
            AssetDatabase.FindAssets("t:Scene", Root)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !IsTestRunnerScene(path) && !SavePlaySceneCopies.IsCopy(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new SaveSystemSettings.SceneEntry { Guid = AssetDatabase.AssetPathToGUID(path), Path = path })
                .ToList();

        /// <summary>Every asset of the settings' folders, sprites inside textures too; prefabs and scenes have catalogs of their own.</summary>
        public static List<SaveSystemSettings.AssetEntry> BuildAssetEntries(SaveSystemSettings settings)
        {
            string[] folders = settings.AssetFolders.Where(AssetDatabase.IsValidFolder).ToArray();
            var entries = new Dictionary<string, SaveSystemSettings.AssetEntry>();
            if (folders.Length == 0)
                return new List<SaveSystemSettings.AssetEntry>();

            foreach (string path in AssetDatabase.FindAssets("t:Object", folders).Select(AssetDatabase.GUIDToAssetPath).Distinct())
            {
                if (AssetDatabase.IsValidFolder(path) || path.EndsWith(".unity") || path.EndsWith(".prefab")
                    || path.EndsWith(".cs") || path.EndsWith(".asmdef"))
                    continue;

                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset == null || asset is GameObject || asset is Component
                        || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                        continue;

                    string key = SaveSystemSettings.AssetKey(guid, localId, AssetDatabase.IsMainAsset(asset));
                    entries[key] = new SaveSystemSettings.AssetEntry { Key = key, Asset = asset };
                }
            }

            return entries.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToList();
        }

        public static List<SaveSystemSettings> AllSettings() =>
            AssetDatabase.FindAssets($"t:{nameof(SaveSystemSettings)}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SaveSystemSettings>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(settings => settings != null)
                .ToList();

        /// <summary>Prefabs that depend on the Saveable script, found without loading every prefab of the project.</summary>
        public static List<string> PrefabPathsWithSaveable()
        {
            string script = SaveableScriptPath();
            return AssetDatabase.FindAssets("t:Prefab", Root)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.GetDependencies(path, recursive: true).Contains(script))
                .ToList();
        }

        // The Test Framework saves InitTestScene*.unity for every PlayMode run and deletes it afterwards
        private static bool IsTestRunnerScene(string path) =>
            System.IO.Path.GetFileName(path).StartsWith("InitTestScene", StringComparison.Ordinal);

        private static string SaveableScriptPath()
        {
            // HideAndDontSave also tells SaveSystemSetup.EnsureSettingsFor that this Saveable is a helper: keep the flag
            var holder = new GameObject { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                return AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(holder.AddComponent<Saveable>()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static bool SetString(SerializedObject serialized, string name, string value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property.stringValue == value)
                return false;

            property.stringValue = value;
            return true;
        }

        private static bool Same(SaveSystemSettings settings, List<SaveSystemSettings.PrefabEntry> prefabs,
            List<SaveSystemSettings.SceneEntry> scenes) =>
            settings.Prefabs.Count == prefabs.Count && settings.Scenes.Count == scenes.Count
            && settings.Prefabs.Zip(prefabs, (a, b) => a.Id == b.Id && a.Prefab == b.Prefab).All(same => same)
            && settings.Scenes.Zip(scenes, (a, b) => a.Guid == b.Guid && a.Path == b.Path).All(same => same);

        private static bool SameAssets(SaveSystemSettings settings, List<SaveSystemSettings.AssetEntry> assets) =>
            settings.Assets.Count == assets.Count
            && settings.Assets.Zip(assets, (a, b) => a.Key == b.Key && a.Asset == b.Asset).All(same => same);
    }
}
