using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Saving.Objects
{
    /// <summary>
    /// The objects layer inside the service. Writes "scene/&lt;GUID&gt;" sections (Saveables, destroyed and spawned
    /// objects), "$persistent" for DontDestroyOnLoad and "$scenes" with the active scene. A scene that loads gets its
    /// section from the snapshot in phases: objects on sceneLoaded (destroy, spawn, active flags), component data in
    /// each Saveable's late Start (disabled and already started ones at once), then OnAfterLoad. Load reloads the
    /// saved scene, or applies to it in place when it has just loaded and nothing in it has started. LoadInPlace keeps
    /// the scenes in play: the saved one is neither loaded nor required, the active one gets its data in place if it
    /// was scanned this frame and nothing in it has started, the others on their scan.
    /// </summary>
    internal sealed class SceneSaving : ISaveExtension, ISaveObjectCount
    {
        public const string SCENES_KEY = "$scenes";
        public const string PERSISTENT_KEY = "$persistent";
        public const string SCENE_PREFIX = "scene/";

        private const int SECTION_VERSION = 1;
        private const string PERSISTENT_SCENE = "DontDestroyOnLoad";

        private readonly SaveLoadService _service;
        private readonly SaveSystemSettings _settings;
        private readonly ComponentSerializerCatalog _catalog;
        private readonly Dictionary<Saveable, ObjectData> _pending = new();
        private readonly List<Saveable> _restored = new();
        private readonly HashSet<string> _warned = new();
        // Whole scene: saved copies of prefabs without a Saveable wait until the scene's own scripts have started,
        // so an object the game makes again itself (a weapon given in Start) is taken over, not made twice
        private readonly Dictionary<Scene, List<SpawnedData>> _byName = new();

        private Action _finishLoad;
        private string _awaitedScenePath;
        // The scenes saved next to the active one: asked for once the active one has started, awaited until scanned
        private List<string> _additiveToRestore;
        private readonly HashSet<string> _awaitedAdditive = new();
        private bool _additivePending;
        private int _loadRun;

        /// <summary>Scene objects (of the scenes and made in play) the last capture wrote.</summary>
        public int LastCapturedObjects { get; private set; }

        public SceneSaving(SaveLoadService service, SaveSystemSettings settings, ComponentSerializerCatalog catalog)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _settings = settings ? settings : throw new ArgumentNullException(nameof(settings));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            SaveRegistry.SceneScanned += OnSceneScanned;
            SaveRegistry.Ready += OnReady;
            SaveReferences.Settings = settings;
            SaveMembers.SetThirdPartyAssemblies(settings.ThirdPartyAssemblies);
        }

        public void Dispose()
        {
            SaveRegistry.SceneScanned -= OnSceneScanned;
            SaveRegistry.Ready -= OnReady;
            _additivePending = false;
            _loadRun++;
            if (SaveReferences.Settings == _settings)
                SaveReferences.Settings = null;
        }

        // ---------- Save ----------

        public void CaptureInto(IDictionary<string, SaveSection> sections)
        {
            LastCapturedObjects = 0;
            WholeScene.AdoptMadeInPlay(_catalog);
            List<Saveable> live = SaveRegistry.All();
            foreach (Saveable saveable in live)
                Callbacks(saveable, beforeSave: true);

            foreach (SaveRegistry.SceneRecord record in SaveRegistry.Records())
            {
                if (!record.Scene.isLoaded)
                    continue;

                List<Saveable> inScene = SaveRegistry.InScene(record.Scene);
                if (!TryGetGuid(record.Scene, out string guid))
                {
                    if (inScene.Count > 0)
                        WarnOnce($"scene/{record.Scene.path}", $"The scene '{record.Scene.path}' is not in SaveSystemSettings, " +
                                                               "its objects are not saved: Tools → Save System → Rebuild Catalogs.");
                    continue;
                }

                string key = SCENE_PREFIX + guid;
                sections[key] = Section(CaptureObjects(inScene, record.Originals, ReadScene(sections, key)));
            }

            List<Saveable> persistent = live.Where(saveable => saveable.gameObject.scene.name == PERSISTENT_SCENE).ToList();
            if (persistent.Count > 0 || sections.ContainsKey(PERSISTENT_KEY))
                sections[PERSISTENT_KEY] = Section(CaptureObjects(persistent, null, ReadScene(sections, PERSISTENT_KEY)));

            Scene active = SceneManager.GetActiveScene();
            if (TryGetGuid(active, out string activeGuid))
            {
                sections[SCENES_KEY] = Section(new ScenesData
                {
                    Active = new SceneRef { Path = SaveSceneCopies.ToSave(active.path), Guid = activeGuid },
                    Additive = CaptureAdditive(active)
                });
            }
        }

        /// <summary>The scenes loaded next to the active one, in their order; null when there are none.</summary>
        private List<SceneRef> CaptureAdditive(Scene active)
        {
            List<SceneRef> additive = null;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene == active || !scene.isLoaded || !TryGetGuid(scene, out string guid))
                    continue;

                (additive ??= new List<SceneRef>()).Add(new SceneRef { Path = SaveSceneCopies.ToSave(scene.path), Guid = guid });
            }

            return additive;
        }

        private SceneData CaptureObjects(List<Saveable> saveables, HashSet<string> originals, SceneData previous)
        {
            var data = new SceneData();
            var live = new HashSet<string>();
            foreach (Saveable saveable in saveables.OrderBy(saveable => saveable.Id, StringComparer.Ordinal))
            {
                live.Add(saveable.Id);
                if (!saveable.IsSpawned)
                {
                    ObjectData old = previous != null && previous.Objects.TryGetValue(saveable.Id, out ObjectData found) ? found : null;
                    ObjectData captured = CaptureObject(saveable, old);
                    // A whole-scene object that stands as the scene has it: nothing to write, the scene brings it back
                    if (saveable.Baseline is { } baseline && captured.Active == baseline.Active
                                                          && captured.Components.Count == 0 && captured.Children.Count == 0)
                        continue;

                    data.Objects[saveable.Id] = captured;
                }
                else if (IsTopSpawned(saveable) && saveable.gameObject.activeInHierarchy)
                {
                    SpawnedData spawned = CaptureSpawned(saveable, previous);
                    if (spawned != null)
                        data.Spawned.Add(spawned);
                }
            }

            LastCapturedObjects += data.Objects.Count + data.Spawned.Count;

            if (originals != null)
                data.Destroyed.AddRange(originals.Where(id => !live.Contains(id)).OrderBy(id => id, StringComparer.Ordinal));

            return data;
        }

        private ObjectData CaptureObject(Saveable saveable, ObjectData previous) =>
            _pending.TryGetValue(saveable, out ObjectData waiting) ? waiting : ObjectState.Capture(saveable, _catalog, previous);

        private SpawnedData CaptureSpawned(Saveable saveable, SceneData previous)
        {
            if (string.IsNullOrEmpty(saveable.PrefabId))
            {
                WarnOnce($"no-prefab/{saveable.Id}", $"'{saveable.name}' was made in play from an object without a prefab id; " +
                                                     "it cannot be restored and is not saved.", saveable);
                return null;
            }

            SpawnedData old = previous?.Spawned.FirstOrDefault(entry => entry.Id == saveable.Id);
            var spawned = new SpawnedData { Id = saveable.Id, Prefab = saveable.PrefabId };
            SetParent(saveable, spawned);
            spawned.CopyFrom(CaptureObject(saveable, old));

            foreach (Saveable nested in saveable.GetComponentsInChildren<Saveable>(true))
            {
                if (nested == saveable)
                    continue;

                // What the game added under it in play is saved as an object of its own, with everything below it
                if (IsUnderOwnSpawn(nested, saveable))
                    continue;

                string path = ComponentKeys.PathOf(saveable.transform, nested.transform);
                NestedData oldNested = old != null && old.Nested.TryGetValue(path, out NestedData found) ? found : null;
                var entry = new NestedData { Id = nested.Id };
                entry.CopyFrom(CaptureObject(nested, oldNested));
                spawned.Nested[path] = entry;
            }

            return spawned;
        }

        private static void SetParent(Saveable saveable, SpawnedData spawned)
        {
            Transform parent = saveable.transform.parent;
            if (parent == null)
                return;

            Saveable owner = parent.GetComponentInParent<Saveable>(true);
            if (owner != null)
            {
                spawned.Parent = owner.Id;
                spawned.ParentPath = ComponentKeys.PathOf(owner.transform, parent);
            }
            else
            {
                spawned.ParentPath = ComponentKeys.ScenePathOf(parent);
            }
        }

        private static bool IsUnderOwnSpawn(Saveable nested, Saveable root)
        {
            for (Transform node = nested.transform; node != null && node != root.transform; node = node.parent)
            {
                if (node.TryGetComponent(out Saveable above) && above.IsOwnSpawn)
                    return true;
            }

            return false;
        }

        private static bool IsTopSpawned(Saveable saveable)
        {
            if (saveable.IsOwnSpawn)
                return true;

            Transform parent = saveable.transform.parent;
            Saveable owner = parent != null ? parent.GetComponentInParent<Saveable>(true) : null;
            return owner == null || !owner.IsSpawned;
        }

        // ---------- Load ----------

        public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem)
        {
            problem = null;
            foreach (KeyValuePair<string, SaveSection> pair in sections)
            {
                bool isScene = pair.Key.StartsWith(SCENE_PREFIX, StringComparison.Ordinal) || pair.Key == PERSISTENT_KEY;
                if (!isScene && pair.Key != SCENES_KEY)
                    continue;

                if (pair.Value.V > SECTION_VERSION)
                {
                    problem = $"'{pair.Key}' was saved with version {pair.Value.V}, newer than {SECTION_VERSION}";
                    return LoadOutcome.NewerVersion;
                }

                if (!isScene)
                    continue;

                if (!TryRead(pair.Value, out SceneData scene))
                {
                    problem = $"'{pair.Key}' cannot be read";
                    return LoadOutcome.Failed;
                }

                if (FindNewerComponent(scene, out string newer))
                {
                    problem = $"'{pair.Key}': {newer}";
                    return LoadOutcome.NewerVersion;
                }
            }

            // In place the saved scene is not loaded, so it need not exist any more
            if (_service.KeepsScene)
                return LoadOutcome.Loaded;

            if (!sections.TryGetValue(SCENES_KEY, out SaveSection scenesSection)
                || !TryRead(scenesSection, out ScenesData scenes) || scenes.Active == null)
                return LoadOutcome.Loaded;

            if (!SceneExists(PathOf(scenes.Active)))
            {
                problem = $"the saved scene '{scenes.Active.Path}' is not in the build";
                return LoadOutcome.SceneNotFound;
            }

            return LoadOutcome.Loaded;
        }

        public void BeginLoad(Action finished)
        {
            // Read once: restoring DontDestroyOnLoad objects may finish the load early, and the finish clears the flag
            bool keepsScene = _service.KeepsScene;
            bool atSceneStart = _service.AtSceneStart;
            bool live = _service.RestoresLive;
            _finishLoad = finished;
            // A load that begins while another still waits for its scenes takes over
            _additiveToRestore = null;
            _additivePending = false;
            _awaitedAdditive.Clear();
            _loadRun++;
            ApplyPersistent();

            if (keepsScene)
            {
                // The scenes in play stay: the active one gets its data now if it was scanned this frame and nothing in
                // it has started; a scene still loading and every later one get theirs on their scan
                Scene current = SceneManager.GetActiveScene();
                if (SaveRegistry.TryGetRecord(current, out SaveRegistry.SceneRecord fresh) && fresh.IsFresh)
                    ApplyScene(current, fresh);

                ApplyScenesLoadedAlong(current, atSceneStart: false);
                TryFinish();
                return;
            }

            if (live)
            {
                if (CanRestoreLive(out string why))
                {
                    RestoreLive();
                    TryFinish();
                    return;
                }

                Debug.Log($"Save System: this load cannot keep the scenes in play ({why}); the saved scene is loaded again.");
            }

            if (!_service.TryGetSection(SCENES_KEY, out SaveSection section) || !TryRead(section, out ScenesData scenes)
                || scenes.Active == null)
            {
                TryFinish();
                return;
            }

            string path = PathOf(scenes.Active);
            Scene active = SceneManager.GetActiveScene();
            if (SaveSceneCopies.ToSave(active.path) == path && SaveRegistry.TryGetRecord(active, out SaveRegistry.SceneRecord record)
                && (record.IsFresh || (atSceneStart && record.IsStarting)))
            {
                ApplyScene(active, record);
                ApplyScenesLoadedAlong(active, atSceneStart);
                TryFinish();
                return;
            }

            _awaitedScenePath = path;
            _additiveToRestore = AdditivePaths(scenes, path);
            _awaitedAdditive.Clear();
            _loadRun++;
            try
            {
                LoadScene(path, LoadSceneMode.Single);
            }
            catch
            {
                _awaitedScenePath = null;
                _additiveToRestore = null;
                _finishLoad = null;
                throw;
            }
        }

        // ---------- Live load: the scenes in play stay loaded ----------

        /// <summary>
        /// A live load reaches the save only when the scenes are the saved ones and nothing has to come back that only
        /// the scene file can give: an object of the scene itself that was alive in the save and is destroyed now.
        /// </summary>
        private bool CanRestoreLive(out string why)
        {
            why = null;
            if (_service.TryGetSection(SCENES_KEY, out SaveSection scenesSection) && TryRead(scenesSection, out ScenesData scenes)
                                                                                 && scenes.Active != null)
            {
                string active = PathOf(scenes.Active);
                if (SaveSceneCopies.ToSave(SceneManager.GetActiveScene().path) != active)
                {
                    why = $"the save is of '{active}'";
                    return false;
                }

                foreach (SceneRef additive in scenes.Additive ?? Enumerable.Empty<SceneRef>())
                {
                    string path = additive != null ? PathOf(additive) : null;
                    if (path != null && SceneExists(path) && !IsLoaded(path))
                    {
                        why = $"the saved scene '{path}' is not loaded";
                        return false;
                    }
                }
            }

            foreach (SaveRegistry.SceneRecord record in SaveRegistry.Records())
            {
                Scene scene = record.Scene;
                if (!scene.isLoaded || !TryGetGuid(scene, out string guid) || !_service.TryGetSection(SCENE_PREFIX + guid, out SaveSection section))
                    continue;

                if (!TryRead(section, out SceneData data))
                {
                    why = $"the saved data of '{scene.name}' cannot be read";
                    return false;
                }

                var destroyed = new HashSet<string>(data.Destroyed, StringComparer.Ordinal);
                foreach (string id in record.Originals)
                {
                    if (!destroyed.Contains(id) && !SaveRegistry.TryGet(id, out _))
                    {
                        why = $"an object of '{scene.name}' that was destroyed since the save must come back";
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsLoaded(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && SaveSceneCopies.ToSave(scene.path) == path)
                    return true;
            }

            return false;
        }

        private void RestoreLive()
        {
            // What was made since the save and carries state gets its hidden Saveable now, so it is told apart from
            // what the save holds
            WholeScene.AdoptMadeInPlay(_catalog);
            foreach (SaveRegistry.SceneRecord record in SaveRegistry.Records())
            {
                Scene scene = record.Scene;
                if (!scene.isLoaded || !TryGetGuid(scene, out string guid) || !_service.TryGetSection(SCENE_PREFIX + guid, out SaveSection section))
                    continue;

                if (TryRead(section, out SceneData data))
                    ApplyLive(scene, record, data);
            }
        }

        /// <summary>
        /// The scene as it is goes back to the save: objects of the scene destroyed in the save are destroyed, objects
        /// made after the save leave play (the game's pool takes them back), saved ones that are gone are made again,
        /// and everything alive gets its saved data — or, where the save holds none because the object stood as the
        /// scene has it, its place and active flag of the scene's start.
        /// </summary>
        private void ApplyLive(Scene scene, SaveRegistry.SceneRecord record, SceneData data)
        {
            record.Applied = true;
            foreach (string id in data.Destroyed)
            {
                if (SaveRegistry.TryGet(id, out Saveable doomed) && doomed.gameObject.scene == scene && !doomed.IsSpawned)
                {
                    doomed.gameObject.SetActive(false);
                    Object.Destroy(doomed.gameObject);
                }
            }

            var saved = new HashSet<string>(data.Spawned.Select(spawned => spawned.Id), StringComparer.Ordinal);
            List<Saveable> inScene = SaveRegistry.InScene(scene);
            // Twice: a child is matched by its parent, and the parent may have taken its saved id only in the first pass
            TakeSavedIds(scene, data, inScene, saved);
            TakeSavedIds(scene, data, inScene, saved);
            foreach (Saveable saveable in inScene)
            {
                if (saveable == null || !saveable.IsSpawned || !IsTopSpawned(saveable))
                    continue;

                if (!saveable.gameObject.activeInHierarchy)
                {
                    // Out of play (back in its pool) with the id of a saved object: the id goes to the one that comes back
                    if (saved.Contains(saveable.Id))
                        SaveRegistry.Rekey(saveable, SaveRegistry.NewId());
                }
                else if (!saved.Contains(saveable.Id))
                {
                    Remove(saveable.gameObject, saveable.IsOwnSpawn);
                }
            }

            List<SpawnedData> byName = null;
            foreach (SpawnedData spawned in data.Spawned)
            {
                if (SaveRegistry.TryGet(spawned.Id, out Saveable alive) && alive.gameObject.activeInHierarchy)
                {
                    AssignNested(alive, spawned);
                    Assign(alive, spawned);
                }
                else if (IsCopyByName(spawned))
                {
                    (byName ??= new List<SpawnedData>()).Add(spawned);
                }
                else
                {
                    Spawn(spawned, scene);
                }
            }

            foreach (Saveable saveable in inScene)
            {
                if (saveable == null || saveable.IsSpawned || saveable.gameObject.scene != scene)
                    continue;

                data.Objects.TryGetValue(saveable.Id, out ObjectData objectData);
                BackToBaseline(saveable, objectData);
                if (objectData != null)
                    Assign(saveable, objectData);
            }

            if (byName != null)
                SpawnByName(scene, byName);
        }

        /// <summary>
        /// A whole-scene object the save says nothing about (or nothing about its place) stood as the scene has it
        /// when the game was saved: that is where it goes back to.
        /// </summary>
        private static void BackToBaseline(Saveable saveable, ObjectData saved)
        {
            if (saveable.Baseline is not { } baseline)
                return;

            if (saved == null && saveable.gameObject.activeSelf != baseline.Active)
                saveable.gameObject.SetActive(baseline.Active);

            Transform transform = saveable.transform;
            bool placeSaved = saved != null && saved.Components.ContainsKey(transform.GetType().Name);
            if (placeSaved || saveable.IsAnimated || baseline.SameTransform(transform))
                return;

            baseline.ApplyTransform(transform);
            TransformSerializer.TellMovers(transform);
        }

        /// <summary>
        /// An object in play that the save holds under another id: the game made it again on its own after the save was
        /// loaded (a weapon its owner is given a moment after a restart), so it never took the saved id. A saved copy of
        /// the same prefab under the same parent that is not in play is that object — it takes the id instead of being
        /// removed and made anew.
        /// </summary>
        private void TakeSavedIds(Scene scene, SceneData data, List<Saveable> inScene, HashSet<string> saved)
        {
            List<SpawnedData> missing = null;
            foreach (SpawnedData spawned in data.Spawned)
            {
                if (!SaveRegistry.TryGet(spawned.Id, out Saveable alive) || !alive.gameObject.activeInHierarchy)
                    (missing ??= new List<SpawnedData>()).Add(spawned);
            }

            if (missing == null)
                return;

            foreach (Saveable saveable in inScene)
            {
                if (saveable == null || !saveable.IsSpawned || !IsTopSpawned(saveable) || saved.Contains(saveable.Id)
                    || !saveable.gameObject.activeInHierarchy || string.IsNullOrEmpty(saveable.PrefabId))
                    continue;

                Transform parent = saveable.transform.parent;
                int match = missing.FindIndex(spawned => spawned.Prefab == saveable.PrefabId && FindParent(spawned, scene) == parent);
                if (match < 0)
                    continue;

                // The id may sit on an object that is out of play (back in its pool): it gives the id up
                if (SaveRegistry.TryGet(missing[match].Id, out Saveable holder) && holder != saveable)
                    SaveRegistry.Rekey(holder, SaveRegistry.NewId());

                SaveRegistry.Rekey(saveable, missing[match].Id);
                missing.RemoveAt(match);
                if (missing.Count == 0)
                    return;
            }
        }

        /// <summary>
        /// An object made after the save leaves play: the game's pool takes it back, or it is destroyed. Under another
        /// object made in play, with a handler set, what the handler does not take is left alone: equipment and UI the
        /// game builds for its owner are the game's, and destroying them breaks the owner.
        /// </summary>
        private static void Remove(GameObject target, bool underCopy)
        {
            bool taken = false;
            Func<GameObject, bool> handler = SaveLoadSystem.DespawnHandler;
            if (handler != null)
            {
                try
                {
                    taken = handler(target);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, target);
                }
            }

            if (taken || target == null || (underCopy && handler != null))
                return;

            target.SetActive(false);
            Object.Destroy(target);
        }

        /// <summary>
        /// A load in place: the scenes that came up together with the active one (additive ones, scenes opened side by
        /// side in the editor) were scanned before the load had anything to give them, so they get their data now —
        /// under the same condition as the active one: just loaded, not restored yet.
        /// </summary>
        private void ApplyScenesLoadedAlong(Scene active, bool atSceneStart)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene == active || !scene.isLoaded || !SaveRegistry.TryGetRecord(scene, out SaveRegistry.SceneRecord record))
                    continue;

                if (record.IsFresh || (atSceneStart && record.IsStarting))
                    ApplyScene(scene, record);
            }
        }

        /// <summary>The saved additive scenes that still exist, without the active one and without repeats.</summary>
        private List<string> AdditivePaths(ScenesData scenes, string activePath)
        {
            if (scenes.Additive == null)
                return null;

            List<string> paths = null;
            foreach (SceneRef scene in scenes.Additive)
            {
                if (scene == null)
                    continue;

                string path = PathOf(scene);
                if (path == activePath || (paths != null && paths.Contains(path)))
                    continue;

                if (!SceneExists(path))
                {
                    WarnOnce($"additive/{path}", $"The saved scene '{path}' is not in the build any more; the load goes on without it.");
                    continue;
                }

                (paths ??= new List<string>()).Add(path);
            }

            return paths;
        }

        /// <summary>
        /// The saved scene is back: once its scripts have started, the scenes that were loaded next to it come back
        /// too — those the game has not loaded (or begun to load) itself. A game that composes its scenes from
        /// outside (a launcher, a bootstrap that is gone by now) gets them from here; a scene that loads its
        /// companions in Awake or Start is not given a second copy.
        /// </summary>
        private async void RestoreAdditiveScenes(List<string> paths, int run)
        {
            try
            {
                await Awaitable.NextFrameAsync();
            }
            catch (OperationCanceledException)
            {
                return; // Play ended
            }

            if (run != _loadRun || !_additivePending)
                return; // another load began, or the layer is gone

            _additivePending = false;
            try
            {
                foreach (string path in paths)
                {
                    if (IsInPlay(path))
                        continue;

                    _awaitedAdditive.Add(path);
                    LoadScene(path, LoadSceneMode.Additive);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _awaitedAdditive.Clear();
            }

            TryFinish();
        }

        /// <summary>Loaded or loading: Unity lists a scene from the moment its load is asked for.</summary>
        private static bool IsInPlay(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SaveSceneCopies.ToSave(SceneManager.GetSceneAt(i).path) == path)
                    return true;
            }

            return false;
        }

        private void OnSceneScanned(Scene scene)
        {
            string scanned = SaveSceneCopies.ToSave(scene.path);
            if (_awaitedScenePath != null && scanned == _awaitedScenePath)
            {
                _awaitedScenePath = null;
                if (_additiveToRestore != null)
                {
                    _additivePending = true;
                    RestoreAdditiveScenes(_additiveToRestore, _loadRun);
                    _additiveToRestore = null;
                }
            }

            _awaitedAdditive.Remove(scanned);

            if (SaveRegistry.TryGetRecord(scene, out SaveRegistry.SceneRecord record))
                ApplyScene(scene, record);

            TryFinish();
        }

        private void ApplyScene(Scene scene, SaveRegistry.SceneRecord record)
        {
            if (!TryGetGuid(scene, out string guid) || !_service.TryGetSection(SCENE_PREFIX + guid, out SaveSection section))
                return;

            if (!TryRead(section, out SceneData data))
            {
                Debug.LogError($"The saved data of scene '{scene.path}' cannot be read; the scene stays as it is.");
                return;
            }

            record.Applied = true;
            foreach (string id in data.Destroyed)
            {
                if (SaveRegistry.TryGet(id, out Saveable doomed) && doomed.gameObject.scene == scene && !doomed.IsSpawned)
                {
                    // Switched off first: its Start must not run in the frame before Destroy takes effect
                    doomed.gameObject.SetActive(false);
                    Object.Destroy(doomed.gameObject);
                }
            }

            List<SpawnedData> byName = null;
            foreach (SpawnedData spawned in data.Spawned)
            {
                if (IsCopyByName(spawned))
                    (byName ??= new List<SpawnedData>()).Add(spawned);
                else
                    Spawn(spawned, scene);
            }

            foreach (KeyValuePair<string, ObjectData> pair in data.Objects)
            {
                if (SaveRegistry.TryGet(pair.Key, out Saveable saveable) && saveable.gameObject.scene == scene && !saveable.IsSpawned)
                    Assign(saveable, pair.Value);
            }

            if (byName == null)
                return;

            // The scene's scripts have started already (Load On Start a frame in): what they made is there to take over
            if (record.AnyStarted)
                SpawnByName(scene, byName);
            else
                _byName[scene] = byName;
        }

        /// <summary>A saved copy of a prefab that has no Saveable: the whole-scene scope knows the prefab by its id.</summary>
        private bool IsCopyByName(SpawnedData spawned) =>
            !_settings.TryGetPrefab(spawned.Prefab, out _) && WholeScene.TryGetPrefab(spawned.Prefab, out _);

        private void SpawnWaitingByName()
        {
            if (_byName.Count == 0)
                return;

            var waiting = new List<KeyValuePair<Scene, List<SpawnedData>>>(_byName);
            _byName.Clear();
            foreach (KeyValuePair<Scene, List<SpawnedData>> pair in waiting)
            {
                if (pair.Key.isLoaded)
                    SpawnByName(pair.Key, pair.Value);
            }
        }

        private void SpawnByName(Scene scene, List<SpawnedData> copies)
        {
            SavePolicy policy = WholeScene.PolicyOf(scene);
            foreach (SpawnedData spawned in ParentsFirst(copies))
            {
                if (policy == null || !WholeScene.TryGetPrefab(spawned.Prefab, out GameObject prefab))
                {
                    WarnOnce($"prefab/{spawned.Prefab}", $"The saved object {spawned.Id} was made in play in a scene saved whole; " +
                                                         $"'{scene.name}' is not saved that way any more, so it is skipped.");
                    continue;
                }

                if (SaveRegistry.TryGet(spawned.Id, out _))
                    continue; // alive already

                Transform parent = FindParent(spawned, scene);
                GameObject instance = WholeScene.FindMadeInPlay(scene, parent, prefab);
                if (instance == null)
                {
                    // Under another copy (a health bar, a weapon in a hand, a rocket): the game builds most of these
                    // itself whenever it builds the parent, some of them later than now. With a handler set, only
                    // what the game claims is made — an object instantiated past its DI container would be a broken
                    // twin of the one the game is about to make
                    bool claimedOnly = !string.IsNullOrEmpty(spawned.Parent) && SaveLoadSystem.SpawnHandler != null;
                    instance = Make(prefab, parent, claimedOnly);
                    if (instance == null)
                        continue;

                    if (instance.transform.parent == null && instance.scene != scene)
                        SceneManager.MoveGameObjectToScene(instance, scene);

                    // An effect: the game's own timer that would destroy it did not come back with it
                    if (!SavePolicy.HasState(instance, _catalog))
                        SavedEffect.Watch(instance);
                }

                // A pool hands out an object that was in play before: it still has the hidden Saveable of that life
                Saveable root = instance.TryGetComponent(out Saveable earlier)
                    ? earlier
                    : Saveable.Adopt(instance, spawned.Id, policy, null, spawned.Prefab);
                root.IsOwnSpawn = !string.IsNullOrEmpty(spawned.Parent);
                SaveRegistry.Rekey(root, spawned.Id);
                Assign(root, spawned);
            }
        }

        /// <summary>A copy saved under another copy (a rocket under its robot) is made after the one it hangs on.</summary>
        private static List<SpawnedData> ParentsFirst(List<SpawnedData> copies)
        {
            var ids = new HashSet<string>(copies.Select(copy => copy.Id), StringComparer.Ordinal);
            var placed = new HashSet<string>(StringComparer.Ordinal);
            var ordered = new List<SpawnedData>(copies.Count);
            var waiting = new List<SpawnedData>(copies);
            while (waiting.Count > 0)
            {
                int before = waiting.Count;
                for (int index = 0; index < waiting.Count; index++)
                {
                    SpawnedData copy = waiting[index];
                    if (!string.IsNullOrEmpty(copy.Parent) && ids.Contains(copy.Parent) && !placed.Contains(copy.Parent))
                        continue;

                    ordered.Add(copy);
                    placed.Add(copy.Id);
                    waiting.RemoveAt(index--);
                }

                // A loop of parents cannot be: whatever is left goes as it is
                if (waiting.Count == before)
                {
                    ordered.AddRange(waiting);
                    break;
                }
            }

            return ordered;
        }

        private void ApplyPersistent()
        {
            if (!_service.TryGetSection(PERSISTENT_KEY, out SaveSection section) || !TryRead(section, out SceneData data))
                return;

            foreach (SpawnedData spawned in data.Spawned)
            {
                if (SaveRegistry.TryGet(spawned.Id, out Saveable alive))
                {
                    AssignNested(alive, spawned);
                    Assign(alive, spawned);
                }
                else
                {
                    Spawn(spawned, default);
                }
            }

            foreach (KeyValuePair<string, ObjectData> pair in data.Objects)
            {
                if (SaveRegistry.TryGet(pair.Key, out Saveable saveable))
                    Assign(saveable, pair.Value);
            }
        }

        /// <summary>Makes a saved spawned object again; an invalid scene means DontDestroyOnLoad.</summary>
        private void Spawn(SpawnedData spawned, Scene scene)
        {
            if (!_settings.TryGetPrefab(spawned.Prefab, out GameObject prefab))
            {
                WarnOnce($"prefab/{spawned.Prefab}", $"The prefab {spawned.Prefab} of the spawned object {spawned.Id} is not in " +
                                                     "SaveSystemSettings; the object is skipped.");
                return;
            }

            Transform parent = FindParent(spawned, scene);
            GameObject instance = Make(prefab, parent);
            if (instance == null)
                return;

            if (instance.transform.parent == null)
            {
                if (!scene.IsValid())
                    Object.DontDestroyOnLoad(instance);
                else if (instance.scene != scene)
                    SceneManager.MoveGameObjectToScene(instance, scene);
            }

            if (!instance.TryGetComponent(out Saveable root))
            {
                Debug.LogError($"The prefab '{prefab.name}' has no Saveable on its root any more; the spawned object is skipped.");
                Object.Destroy(instance);
                return;
            }

            SaveRegistry.Rekey(root, spawned.Id);
            AssignNested(root, spawned);
            Assign(root, spawned);
        }

        private void AssignNested(Saveable root, SpawnedData spawned)
        {
            foreach (KeyValuePair<string, NestedData> pair in spawned.Nested)
            {
                Transform node = ComponentKeys.FindChild(root.transform, pair.Key);
                if (node == null || !node.TryGetComponent(out Saveable nested))
                {
                    WarnOnce($"nested/{spawned.Prefab}/{pair.Key}", $"The prefab of '{root.name}' has no Saveable at {pair.Key} any " +
                                                                   "more; its saved data is skipped.", root);
                    continue;
                }

                if (nested.Id != pair.Value.Id)
                    SaveRegistry.Rekey(nested, pair.Value.Id);

                Assign(nested, pair.Value);
            }
        }

        /// <summary>
        /// A saved object made again: by the game's own handler (a DI container, a pool) or by Object.Instantiate.
        /// Null when the handler threw — a half-made object is worse than a missing one.
        /// </summary>
        private static GameObject Make(GameObject prefab, Transform parent, bool claimedOnly = false)
        {
            Func<GameObject, Transform, GameObject> handler = SaveLoadSystem.SpawnHandler;
            if (handler == null)
                return Object.Instantiate(prefab, parent);

            GameObject instance;
            try
            {
                instance = handler(prefab, parent);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, prefab);
                Debug.LogError($"SaveLoadSystem.SpawnHandler failed for the prefab '{prefab.name}'; the saved object is skipped.", prefab);
                return null;
            }

            // Not the game's to make (an effect, a prop): the usual way
            if (instance == null)
                return claimedOnly ? null : Object.Instantiate(prefab, parent);

            // A pool keeps its objects in a folder of its own: fine while the save names no parent
            if (parent != null && instance.transform.parent != parent)
                instance.transform.SetParent(parent, false);

            return instance;
        }

        private static Transform FindParent(SpawnedData spawned, Scene scene)
        {
            if (!string.IsNullOrEmpty(spawned.Parent))
            {
                if (!SaveRegistry.TryGet(spawned.Parent, out Saveable owner))
                    return null;

                return string.IsNullOrEmpty(spawned.ParentPath)
                    ? owner.transform
                    : ComponentKeys.FindChild(owner.transform, spawned.ParentPath);
            }

            return scene.IsValid() ? ComponentKeys.FindInScene(scene, spawned.ParentPath) : null;
        }

        private void Assign(Saveable saveable, ObjectData data)
        {
            ObjectState.RestoreActive(saveable, data);
            if (saveable.HasStarted || !saveable.gameObject.activeInHierarchy)
                Apply(saveable, data);
            else
                _pending[saveable] = data;
        }

        private void Apply(Saveable saveable, ObjectData data)
        {
            ObjectState.RestoreComponents(saveable, data, _catalog);
            _restored.Add(saveable);
        }

        private void OnReady(Saveable saveable)
        {
            // The first Saveable to start: every script of the scene has started before it, so what they made is there
            SpawnWaitingByName();
            if (!_pending.Remove(saveable, out ObjectData data))
                return;

            Apply(saveable, data);
            TryFinish();
        }

        /// <summary>When nothing waits any more: OnAfterLoad for what was restored, then the load ends.</summary>
        private void TryFinish()
        {
            foreach (Saveable gone in _pending.Keys.Where(saveable => saveable == null).ToList())
                _pending.Remove(gone);

            if (_pending.Count > 0 || _awaitedScenePath != null || _additivePending || _awaitedAdditive.Count > 0)
                return;

            // Nothing in the scene waits for its Start: the copies by name are made now
            if (_byName.Count > 0)
            {
                SpawnWaitingByName();
                if (_pending.Count > 0)
                    return;
            }

            foreach (Saveable saveable in _restored)
            {
                if (saveable != null)
                    Callbacks(saveable, beforeSave: false);
            }

            _restored.Clear();
            Action finish = _finishLoad;
            _finishLoad = null;
            finish?.Invoke();
        }

        // ---------- Helpers ----------

        private static void Callbacks(Saveable saveable, bool beforeSave)
        {
            foreach (KeyedComponent entry in ComponentKeys.Collect(saveable))
            {
                if (entry.Component is not ISaveCallbacks callbacks)
                    continue;

                try
                {
                    if (beforeSave)
                        callbacks.OnBeforeSave();
                    else
                        callbacks.OnAfterLoad();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, entry.Component);
                }
            }
        }

        private bool FindNewerComponent(SceneData scene, out string newer)
        {
            IEnumerable<ObjectData> objects = scene.Objects.Values
                .Concat(scene.Spawned)
                .Concat(scene.Spawned.SelectMany(spawned => spawned.Nested.Values));

            foreach (ObjectData data in objects)
            {
                foreach (KeyValuePair<string, ComponentData> pair in data.Components)
                {
                    int current = _catalog.VersionOf(ComponentKeys.TypeNameOf(pair.Key));
                    if (pair.Value.V > current)
                    {
                        newer = $"{pair.Key} was saved with version {pair.Value.V}, newer than {current}";
                        return true;
                    }
                }
            }

            newer = null;
            return false;
        }

        private bool TryGetGuid(Scene scene, out string guid)
        {
            guid = null;
            // In the editor a scene with unsaved changes is played from a copy: it is the original as far as saves go
            return scene.IsValid() && !string.IsNullOrEmpty(scene.path)
                                   && _settings.TryGetSceneGuid(SaveSceneCopies.ToSave(scene.path), out guid);
        }

        private string PathOf(SceneRef scene) => _settings.TryGetScenePath(scene.Guid, out string path) ? path : scene.Path;

        private static bool SceneExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) != null;
#else
            return SceneUtility.GetBuildIndexByScenePath(path) >= 0;
#endif
        }

        private static void LoadScene(string path, LoadSceneMode mode)
        {
#if UNITY_EDITOR
            // The scene as it was when Play started, unsaved changes included — not the file on disk
            path = SaveSceneCopies.ToLoad(path);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(mode));
#else
            SceneManager.LoadScene(path, mode);
#endif
        }

        private static SaveSection Section(object data) => new() { V = SECTION_VERSION, Data = SaveJson.ToToken(data) };

        private static bool TryRead<T>(SaveSection section, out T data) where T : class
        {
            try
            {
                data = SaveJson.FromToken<T>(section?.Data);
            }
            catch (Exception)
            {
                data = null;
            }

            return data != null;
        }

        private static SceneData ReadScene(IDictionary<string, SaveSection> sections, string key) =>
            sections.TryGetValue(key, out SaveSection section) && TryRead(section, out SceneData data) ? data : null;

        private void WarnOnce(string key, string message, Object context = null)
        {
            if (_warned.Add(key))
                Debug.LogWarning(message, context);
        }
    }
}
