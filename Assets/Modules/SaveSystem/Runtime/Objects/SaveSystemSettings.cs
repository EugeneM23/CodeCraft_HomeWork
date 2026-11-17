using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// The project's save settings, set on SaveLoadSystem: plugs in the objects layer and holds its catalogs —
    /// prefabs with a Saveable on the root by prefab id, scenes by GUID and path, and assets of the chosen folders by key
    /// for references. The editor keeps the catalogs up to
    /// date on its own (on import, move and delete, before a build, Tools → Save System → Rebuild Catalogs). A scene
    /// without a SaveSystem gets one made from it when the first Saveable wakes, if the asset lies right in a Resources
    /// folder (CreateSystemWhenMissing).
    /// </summary>
    [CreateAssetMenu(fileName = RESOURCES_NAME, menuName = "Save System/Settings")]
    public sealed class SaveSystemSettings : SaveExtensionAsset
    {
        /// <summary>The name a scene without a SaveSystem finds the settings by: an asset right in a Resources folder.</summary>
        public const string RESOURCES_NAME = "SaveSystemSettings";

        [Serializable]
        public sealed class PrefabEntry
        {
            public string Id;
            public GameObject Prefab;
        }

        [Serializable]
        public sealed class SceneEntry
        {
            public string Guid;
            public string Path;
        }

        [Serializable]
        public sealed class AssetEntry
        {
            public string Key;
            public UnityEngine.Object Asset;
        }

        [Tooltip("A Saveable that finds no SaveSystem in play creates one with default settings. Only an asset named " +
                 RESOURCES_NAME + " right in a Resources folder is found")]
        [SerializeField] private bool _createSystemWhenMissing = true;

        [SerializeField] private List<PrefabEntry> _prefabs = new();
        [SerializeField] private List<SceneEntry> _scenes = new();
        [Tooltip("Folders whose assets [Save] fields and serializers may reference; the table below follows them")]
        [SerializeField] private List<string> _assetFolders = new();
        [SerializeField] private List<AssetEntry> _assets = new();

        [Tooltip("Assemblies of code the game did not write (plugins in Plugins folders, dlls, packages from a registry). " +
                 "Their components are not saved as the game's scripts; the editor keeps the list")]
        [SerializeField] private List<string> _thirdPartyAssemblies = new();

        /// <summary>
        /// Assemblies of third-party code: the whole-scene scope and the All Fields mode leave their components and
        /// the fields of their base classes alone (a DI context, a kernel, a tween keep their own state).
        /// </summary>
        public IReadOnlyList<string> ThirdPartyAssemblies => _thirdPartyAssemblies;

        /// <summary>Replaces the list: the editor fills it from the project's assemblies, tests by hand.</summary>
        public void SetThirdPartyAssemblies(IEnumerable<string> names)
        {
            _thirdPartyAssemblies = names.ToList();
            SaveMembers.SetThirdPartyAssemblies(_thirdPartyAssemblies);
        }

        public IReadOnlyList<PrefabEntry> Prefabs => _prefabs;
        public IReadOnlyList<SceneEntry> Scenes => _scenes;

        /// <summary>A scene without a SaveSystem gets one from this asset (when it lies in a Resources folder).</summary>
        public bool CreateSystemWhenMissing
        {
            get => _createSystemWhenMissing;
            set => _createSystemWhenMissing = value;
        }

        public IReadOnlyList<string> AssetFolders => _assetFolders;
        public IReadOnlyList<AssetEntry> Assets => _assets;

        [NonSerialized] private Dictionary<string, UnityEngine.Object> _assetsByKey;
        [NonSerialized] private Dictionary<UnityEngine.Object, string> _keysByAsset;

        /// <summary>An asset's key in the table: its GUID; for one inside another (a sprite of a texture), GUID:localId.</summary>
        public static string AssetKey(string guid, long localId, bool isMainAsset) =>
            isMainAsset ? guid : $"{guid}:{localId}";

        public bool TryGetAsset(string key, out UnityEngine.Object asset)
        {
            BuildLookups();
            asset = null;
            return !string.IsNullOrEmpty(key) && _assetsByKey.TryGetValue(key, out asset) && asset != null;
        }

        public bool TryGetAssetKey(UnityEngine.Object asset, out string key)
        {
            BuildLookups();
            key = null;
            return asset != null && _keysByAsset.TryGetValue(asset, out key);
        }

        public void SetAssetFolders(IEnumerable<string> folders) => _assetFolders = folders.ToList();

        /// <summary>Replaces the asset table: the editor fills it from the folders, tests by hand.</summary>
        public void SetAssetCatalog(IEnumerable<AssetEntry> assets)
        {
            _assets = assets.ToList();
            ForgetLookups();
        }

        private void OnValidate() => ForgetLookups();

        private void ForgetLookups()
        {
            _assetsByKey = null;
            _keysByAsset = null;
        }

        private void BuildLookups()
        {
            if (_assetsByKey != null)
                return;

            _assetsByKey = new Dictionary<string, UnityEngine.Object>();
            _keysByAsset = new Dictionary<UnityEngine.Object, string>();
            foreach (AssetEntry entry in _assets)
            {
                if (entry == null || entry.Asset == null || string.IsNullOrEmpty(entry.Key))
                    continue;

                _assetsByKey[entry.Key] = entry.Asset;
                _keysByAsset[entry.Asset] = entry.Key;
            }
        }

        public override ISaveExtension CreateExtension(SaveLoadService service) =>
            new SceneSaving(service, this, ComponentSerializerCatalog.Default);

        public bool TryGetPrefab(string id, out GameObject prefab)
        {
            prefab = _prefabs.FirstOrDefault(entry => entry.Id == id)?.Prefab;
            return prefab != null;
        }

        public bool TryGetSceneGuid(string path, out string guid)
        {
            guid = _scenes.FirstOrDefault(entry => entry.Path == path)?.Guid;
            return !string.IsNullOrEmpty(guid);
        }

        public bool TryGetScenePath(string guid, out string path)
        {
            path = _scenes.FirstOrDefault(entry => entry.Guid == guid)?.Path;
            return !string.IsNullOrEmpty(path);
        }

        /// <summary>Replaces both catalogs: the editor fills them, tests fill them by hand.</summary>
        public void SetCatalogs(IEnumerable<PrefabEntry> prefabs, IEnumerable<SceneEntry> scenes)
        {
            _prefabs = prefabs.ToList();
            _scenes = scenes.ToList();
        }
    }
}
