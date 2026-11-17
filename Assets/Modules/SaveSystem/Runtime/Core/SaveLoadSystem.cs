using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Saving
{
    /// <summary>
    /// The save system as a component: drop it in a scene (or the SaveSystem prefab) and saving works. Keeps itself
    /// across scene loads when it sits on a root object and is reachable as <see cref="Current"/>. Every other copy —
    /// the prefab in the next scene, the same scene reloaded by a load — stays idle and forwards its calls and events
    /// to Current, so buttons wired straight to the scene's SaveSystem work in every scene and after every load. Owns
    /// the storage picked in the Inspector (files, PlayerPrefs or a custom asset, with
    /// optional compression and encryption) and an optional server sync, selects the startup slot and loads it if asked,
    /// and saves on quit, on app pause or focus loss and on an autosave timer that runs on game time (only when something
    /// changed). Void methods (SaveGame, LoadFromSlot, NewGame, ContinueGame…) are for Button.onClick, UnityEvents report
    /// back. The service starts on first use: <see cref="UseStorage"/>, <see cref="Configure"/> and friends go before
    /// it. A settings asset (SaveSystemSettings) plugs in the objects layer; the editor finds or creates it.
    /// </summary>
    // Before everything else in its scene, DI contexts and bootstraps included (they sit near -10000): what the scene has
    // when this wakes is what its file holds, and what the game makes in Awake counts as made in play
    [DefaultExecutionOrder(-31000)]
    public sealed class SaveLoadSystem : MonoBehaviour, ISaveLoadService
    {
        public const string DEFAULT_FOLDER = "Saves";
        public const float MIN_AUTO_SAVE_PERIOD = 5f;
        private const int MAX_REPORTED_PROBLEMS = 50;
        private const string DONT_DESTROY = "DontDestroyOnLoad";

        [Tooltip("Where slots live: files under persistentDataPath, PlayerPrefs (web, small games) or your own asset")]
        [SerializeField] private SaveStorageKind _storageKind = SaveStorageKind.Files;
        [Tooltip("The folder under persistentDataPath for files, the key prefix for PlayerPrefs")]
        [SerializeField] private string _folder = DEFAULT_FOLDER;
        [Tooltip("A SaveStorageAsset that creates your storage: a database, a platform API")]
        [SerializeField] private SaveStorageAsset _customStorage;
        [Tooltip("GZip every file: JSON shrinks several times")]
        [SerializeField] private bool _compress;
        [Tooltip("AES with this key: players cannot read the files or edit encrypted ones (the key ships with the game). Plain files from before encryption are still read")]
        [SerializeField] private bool _encrypt;
        [SerializeField] private string _encryptionKey;

        [Tooltip("A RemoteSaveStorageAsset: a server, database or cloud that keeps copies of slots")]
        [SerializeField] private RemoteSaveStorageAsset _remote;
        [Tooltip("Send every written save to the server")]
        [SerializeField] private bool _uploadAfterSave = true;

        [SerializeField] private int _startupSlot;
        [Tooltip("Start in the slot saved last; the startup slot when there is none")]
        [SerializeField] private bool _startInLatestSlot;
        [SerializeField] private bool _loadOnStart;
        [Tooltip("On a root object: survive scene loads; copies in other scenes forward their buttons and events to it")]
        [SerializeField] private bool _keepAcrossScenes = true;
        [Tooltip("Scene to load for a new game (name or path in Build Settings); empty reloads the active scene")]
        [SerializeField] private string _newGameScene;
        [Tooltip("A load made in play keeps the scenes loaded: objects go back to their saved state, those made since " +
                 "are removed, missing ones are made again. For games whose systems a scene reload would break (DI " +
                 "containers, pools). The scene is still loaded again when the save is of another scene or an object " +
                 "of the scene destroyed since must come back")]
        [SerializeField] private bool _liveLoad;

        [SerializeField] private bool _saveOnQuit = true;
        [Tooltip("Save on app pause and on focus loss: on mobile and web it can be the last chance")]
        [SerializeField] private bool _saveOnPause = true;
        [Tooltip("Do not save in the middle of what the game has started (a coin flying to the bank that pays when it " +
                 "lands: DOTween tweens with OnComplete). Autosave, keys and buttons wait for it to end (5 s at most), " +
                 "quit and app pause end it at once first, a load drops it. Without DOTween nothing changes")]
        [SerializeField] private bool _finishWorkBeforeSave = true;
        [Tooltip("Seconds of game time between autosaves, only when something changed; 0 turns autosave off")]
        [SerializeField] private float _autoSavePeriod = 30f;

        [Tooltip("Whole Scene: every object of this scene is saved, nothing to add to them. Saveables: only objects " +
                 "with a Saveable component")]
        [SerializeField] private SaveScope _scope = SaveScope.Saveables;
        [Tooltip("Whole Scene: what is taken from an object without a Saveable")]
        [SerializeField] private SaveCoverage _coverage = SaveCoverage.Everything;
        [Tooltip("Whole Scene: a script saved by all its fields gives its private simple fields too (numbers, flags, " +
                 "enums, strings, vectors) — a look angle, a phase, a counter. Uncheck if a script keeps a moment of " +
                 "Time.time in a private field and misbehaves after the game is started again")]
        [SerializeField] private bool _privateFields = true;
        [Tooltip("Whole Scene: leave out objects marked Static (level geometry)")]
        [SerializeField] private bool _skipStatic = true;
        [Tooltip("Whole Scene: objects left out, with their children")]
        [SerializeField] private List<GameObject> _ignored = new();
        [SerializeField, HideInInspector] private List<SceneObjectId> _sceneObjects = new();
        [SerializeField, HideInInspector] private List<ScenePrefab> _scenePrefabs = new();

        [Tooltip("Save and load with keys in play: for trying things out")]
        [SerializeField] private bool _hotkeys;
        [SerializeField] private SaveHotkey _saveKey = SaveHotkey.F5;
        [SerializeField] private SaveHotkey _loadKey = SaveHotkey.F9;

        [Tooltip("Save scene objects that have a Saveable; the editor finds or creates the settings asset below")]
        [SerializeField] private bool _saveSceneObjects = true;
        [Tooltip("SaveSystemSettings: plugs in scene objects (Saveable) and their catalogs; empty saves only global systems")]
        [SerializeField] private SaveExtensionAsset _settings;

        [SerializeField] private UnityEvent _onSaved = new();
        [SerializeField] private UnityEvent _onLoaded = new();
        [SerializeField] private UnityEvent _onSlotsChanged = new();
        [Tooltip("A save or load failed or was refused; the text says why")]
        [SerializeField] private UnityEvent<string> _onFailed = new();

        private ISaveStorage _storage;
        private IRemoteSaveStorage _remoteStorage;
        private SaveLoadService _service;
        private ISaveExtension _extension;
        private SaveSync _sync;
        private float _untilAutoSave;
        private bool _isIdle;
        private SaveLoadSystem _relayed;
        private SceneObjectBaseline[] _baselines = Array.Empty<SceneObjectBaseline>();
        private SaveReport _lastSave;

        /// <summary>The save system in play: the first one awake. For buttons and code without a service locator.</summary>
        public static SaveLoadSystem Current { get; private set; }

        /// <summary>
        /// A copy that gave way to <see cref="Current"/>: it neither loads, autosaves nor saves on quit, and passes
        /// every call and event to Current.
        /// </summary>
        public bool IsIdle => _isIdle;

        /// <summary>The system that does the work: Current for an idle copy, this one otherwise.</summary>
        private SaveLoadSystem Live => _isIdle && Current != null && Current != this ? Current : this;

        private SaveLoadService Service => Live == this ? _service ??= CreateService() : Live.Service;

        /// <summary>The server sync when a remote storage is set (asset or <see cref="UseRemote"/>), otherwise null.</summary>
        public SaveSync Sync
        {
            get
            {
                if (Live != this)
                    return Live.Sync;

                _ = Service;
                return _sync;
            }
        }

        /// <summary>Loads a scene for NewGame: route it through your own loader (fades); empty uses SceneManager.</summary>
        public Action<string> LoadSceneHandler { get; set; }

        /// <summary>
        /// Makes a saved object again from its prefab on a load (the prefab, the parent the save names or null): route
        /// it through your DI container or pool, so the object comes back injected and pooled. Empty, or a null result
        /// for a prefab the game does not make itself, uses Object.Instantiate. An object the handler leaves under
        /// another parent is moved to the saved one; with no saved parent it stays where the handler put it. One for
        /// the whole game: set it before the first load (an installer, a bootstrap), it is cleared when Play starts.
        /// </summary>
        public static Func<GameObject, Transform, GameObject> SpawnHandler { get; set; }

        /// <summary>
        /// Takes an object out of play when a live load removes what was made after the save: give it back to your pool
        /// and return true. Empty, false or an exception — the object is destroyed. One for the whole game, cleared when
        /// Play starts.
        /// </summary>
        public static Func<GameObject, bool> DespawnHandler { get; set; }

        /// <summary>
        /// What the game has started and not finished (see <see cref="IWorkInFlight"/>). DOTween by default; set your
        /// own for another tween library, or null to save at any moment. Back to the default when Play starts.
        /// </summary>
        public static IWorkInFlight WorkInFlight { get; set; } = new DOTweenWork();

        public UnityEvent OnSaved => _onSaved;
        public UnityEvent OnLoaded => _onLoaded;
        public UnityEvent OnSlotsChanged => _onSlotsChanged;
        public UnityEvent<string> OnFailed => _onFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCurrent()
        {
            Current = null;
            SpawnHandler = null;
            DespawnHandler = null;
            WorkInFlight = new DOTweenWork();
            Systems.Clear();
        }

        private static readonly List<SaveLoadSystem> Systems = new();

        /// <summary>The scene this component was loaded with (it may have moved to DontDestroyOnLoad since).</summary>
        public Scene HomeScene { get; private set; }

        /// <summary>The save system that came with the scene, if it has one: its scope and object list are the scene's.</summary>
        public static SaveLoadSystem OfScene(Scene scene)
        {
            foreach (SaveLoadSystem system in Systems)
            {
                if (system != null && system.HomeScene == scene)
                    return system;
            }

            return null;
        }

        // ---------- Setup ----------

        /// <summary>Replaces the storage (tests, other platforms), used as it is. Only before the service starts.</summary>
        public void UseStorage(ISaveStorage storage)
        {
            if (_handedOver)
            {
                Live.UseStorage(storage);
                return;
            }

            EnsureNotStarted(nameof(UseStorage));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>Picks the storage from code like the Inspector does. Only before the service starts.</summary>
        public void ConfigureStorage(SaveStorageKind kind, string folder = DEFAULT_FOLDER, bool compress = false,
            string encryptionKey = null)
        {
            if (_handedOver)
            {
                Live.ConfigureStorage(kind, folder, compress, encryptionKey);
                return;
            }

            EnsureNotStarted(nameof(ConfigureStorage));
            _storageKind = kind;
            _folder = folder;
            _compress = compress;
            _encrypt = !string.IsNullOrEmpty(encryptionKey);
            _encryptionKey = encryptionKey;
            OnValidate();
        }

        /// <summary>Keeps slots in step with a server through <see cref="Sync"/>. Only before the service starts.</summary>
        public void UseRemote(IRemoteSaveStorage remote, bool uploadAfterSave = true)
        {
            if (_handedOver)
            {
                Live.UseRemote(remote, uploadAfterSave);
                return;
            }

            EnsureNotStarted(nameof(UseRemote));
            _remoteStorage = remote ?? throw new ArgumentNullException(nameof(remote));
            _uploadAfterSave = uploadAfterSave;
        }

        /// <summary>Sets the component up from code (tests). Only before the service starts.</summary>
        public void Configure(int startupSlot = 0, bool loadOnStart = false, bool saveOnQuit = true,
            bool saveOnPause = true, float autoSavePeriod = 30f)
        {
            if (_handedOver)
            {
                Live.Configure(startupSlot, loadOnStart, saveOnQuit, saveOnPause, autoSavePeriod);
                return;
            }

            EnsureNotStarted(nameof(Configure));
            _startupSlot = startupSlot;
            _loadOnStart = loadOnStart;
            _saveOnQuit = saveOnQuit;
            _saveOnPause = saveOnPause;
            _autoSavePeriod = autoSavePeriod;
            OnValidate();
            _untilAutoSave = _autoSavePeriod;
        }

        /// <summary>
        /// Set by the scene-object layer: the project's settings (SaveSystemSettings in Resources) for a system that saves
        /// scene objects and has none in its field — a scene from another project or a package keeps working.
        /// </summary>
        public static Func<SaveExtensionAsset> FallbackSettings;

        /// <summary>Replaces the settings asset (tests). Only before the service starts.</summary>
        public void UseSettings(SaveExtensionAsset settings)
        {
            if (_handedOver)
            {
                Live.UseSettings(settings);
                return;
            }

            EnsureNotStarted(nameof(UseSettings));
            _settings = settings;
        }

        // ---------- What is saved of the scene ----------

        /// <summary>Whole Scene or only objects with a Saveable: it belongs to the scene this component sits in.</summary>
        public SaveScope Scope => _scope;

        public SaveCoverage Coverage => _coverage;

        /// <summary>Whole Scene: scripts saved by all their fields give their private simple fields too.</summary>
        public bool PrivateFields
        {
            get => _privateFields;
            set => _privateFields = value;
        }

        public bool SkipStatic => _skipStatic;
        public IReadOnlyList<GameObject> Ignored => _ignored;

        /// <summary>The objects the whole-scene scope saves, with their ids; the editor keeps the list as the scene is saved.</summary>
        public IReadOnlyList<SceneObjectId> SceneObjects => _sceneObjects;

        /// <summary>The prefabs the scene can spawn, for objects made in play (matched by name).</summary>
        public IReadOnlyList<ScenePrefab> ScenePrefabs => _scenePrefabs;

        /// <summary>How each object of <see cref="SceneObjects"/> stood when the scene loaded (same order).</summary>
        public IReadOnlyList<SceneObjectBaseline> Baselines => _baselines;

        /// <summary>Sets the scope from code, as the Inspector does (tests, scenes built by code).</summary>
        public void UseScope(SaveScope scope, SaveCoverage coverage = SaveCoverage.Everything, bool skipStatic = true)
        {
            _scope = scope;
            _coverage = coverage;
            _skipStatic = skipStatic;
        }

        /// <summary>Replaces the object and prefab lists of the whole-scene scope: the editor's baker and tests.</summary>
        public void SetSceneTable(IEnumerable<SceneObjectId> objects, IEnumerable<ScenePrefab> prefabs)
        {
            _sceneObjects = objects != null ? new List<SceneObjectId>(objects) : new List<SceneObjectId>();
            _scenePrefabs = prefabs != null ? new List<ScenePrefab>(prefabs) : new List<ScenePrefab>();
            _baselines = Array.Empty<SceneObjectBaseline>();
        }

        /// <summary>
        /// Remembers how the listed objects stand right now. Awake does it before the scripts of the scene run, so
        /// "unchanged since the scene loaded" means "as in the scene file".
        /// </summary>
        public void CaptureBaselines()
        {
            _baselines = new SceneObjectBaseline[_sceneObjects.Count];
            for (int index = 0; index < _sceneObjects.Count; index++)
            {
                GameObject listed = _sceneObjects[index]?.Object;
                if (listed != null)
                    _baselines[index] = new SceneObjectBaseline(listed);
            }
        }

        private HashSet<GameObject> _atAwake;

        /// <summary>
        /// The objects the scene had when this component woke (it wakes first in its scene): the scene's own, as its
        /// file has them. Null until then. What the game adds in Awake — pools and a player made by a DI context —
        /// is not among them and counts as made in play.
        /// </summary>
        public IReadOnlyCollection<GameObject> ObjectsAtAwake => _atAwake;

        private void CaptureSceneObjects()
        {
            _atAwake = new HashSet<GameObject>();
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    _atAwake.Add(node.gameObject);
            }
        }

        // ---------- Trying things out ----------

        /// <summary>Save and load with keys in play (function keys; both input systems).</summary>
        public bool Hotkeys
        {
            get => _hotkeys;
            set => _hotkeys = value;
        }

        public SaveHotkey SaveKey
        {
            get => _saveKey;
            set => _saveKey = value;
        }

        public SaveHotkey LoadKey
        {
            get => _loadKey;
            set => _loadKey = value;
        }

        /// <summary>What the last Save of this session wrote; null before the first one.</summary>
        public SaveReport LastSave => Live == this ? _lastSave : Live.LastSave;

        /// <summary>
        /// Play time and the autosave timer move only by <see cref="Tick"/> from the owner — a game loop with its own
        /// pause; otherwise Update ticks them with Time.deltaTime.
        /// </summary>
        public bool TickManually
        {
            get => _handedOver ? Live.TickManually : _tickManually;
            set
            {
                if (_handedOver)
                    Live.TickManually = value;
                else
                    _tickManually = value;
            }
        }

        private bool _tickManually;

        /// <summary>Counts play time and runs the autosave timer on game time, so a pause waits.</summary>
        public void Tick(float deltaTime)
        {
            if (_handedOver)
            {
                Live.Tick(deltaTime);
                return;
            }

            if (deltaTime <= 0f)
                return;

            Service.AddPlayTime(deltaTime);

            if (_autoSavePeriod <= 0f)
                return;

            _untilAutoSave -= deltaTime;
            if (_untilAutoSave > 0f)
                return;

            _untilAutoSave = _autoSavePeriod;
            SaveWhenSettled(onlyIfChanged: true);
        }


        // ---------- Not in the middle of what the game has started ----------

        /// <summary>
        /// The id of a tween the game saves itself (DOTween <c>SetId</c>): where it is goes into the save with the
        /// game's own data and it is started again after a load. A save does not wait for it and quit does not finish
        /// it; a load drops it like any other.
        /// </summary>
        public const string WORK_SAVED_BY_GAME = "SavedByGame";

        /// <summary>A save the system asked for waits this long at most for the game's work in flight.</summary>
        public const float MAX_WAIT_FOR_WORK = 5f;

        private bool _saveWaiting;
        private bool _waitingOnlyIfChanged = true;
        private float _waitingSince;

        /// <summary>A save the system asked for itself (autosave, key, button) waits for that one.</summary>
        public bool IsSaveWaiting => Live._saveWaiting;

        /// <summary>
        /// Saves the system makes by itself do not cut into what the game has started (see <see cref="WorkInFlight"/>).
        /// On by default; <see cref="Save"/> from code always saves at once.
        /// </summary>
        public bool FinishWorkBeforeSave
        {
            get => _finishWorkBeforeSave;
            set => _finishWorkBeforeSave = value;
        }

        private bool WorkIsBusy()
        {
            if (!_finishWorkBeforeSave || WorkInFlight == null)
                return false;

            try
            {
                return WorkInFlight.IsBusy;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        private void WithWork(Action<IWorkInFlight> act)
        {
            if (!_finishWorkBeforeSave || WorkInFlight == null)
                return;

            try
            {
                act(WorkInFlight);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        /// <summary>
        /// A save the system decides on (autosave, F5, Save buttons): now if nothing the game started is in flight,
        /// otherwise as soon as it has ended (<see cref="MAX_WAIT_FOR_WORK"/> at most). Several requests make one save.
        /// </summary>
        private void SaveWhenSettled(bool onlyIfChanged)
        {
            if (Live != this)
            {
                Live.SaveWhenSettled(onlyIfChanged);
                return;
            }

            if (!_saveWaiting && !WorkIsBusy())
            {
                Save(onlyIfChanged);
                return;
            }

            if (!_saveWaiting)
                _waitingSince = Time.unscaledTime;

            _saveWaiting = true;
            _waitingOnlyIfChanged &= onlyIfChanged;
        }

        private void SaveIfSettled()
        {
            if (!_saveWaiting || (WorkIsBusy() && Time.unscaledTime - _waitingSince < MAX_WAIT_FOR_WORK))
                return;

            bool onlyIfChanged = _waitingOnlyIfChanged;
            ForgetWaitingSave();
            Save(onlyIfChanged);
        }

        private void ForgetWaitingSave()
        {
            _saveWaiting = false;
            _waitingOnlyIfChanged = true;
        }

        /// <summary>The app is about to stop: what is in flight ends now, so its result is in the save.</summary>
        private void FinishWorkAndSave(bool onlyIfChanged)
        {
            WithWork(work => work.Finish());
            ForgetWaitingSave();
            Save(onlyIfChanged);
        }

        // ---------- ISaveLoadService ----------

        public int CurrentSlot => Service.CurrentSlot;
        public bool IsLoading => Service.IsLoading;
        public bool IsReadOnly => Service.IsReadOnly;
        public double PlayTime => Service.PlayTime;
        public IDictionary<string, string> Summary => Service.Summary;
        public SaveValues Values => Service.Values;

        public string SlotName
        {
            get => Service.SlotName;
            set => Service.SlotName = value;
        }

        public event Action<SaveOutcome> Saved
        {
            add => Service.Saved += value;
            remove => Service.Saved -= value;
        }

        public event Action<LoadOutcome> Loaded
        {
            add => Service.Loaded += value;
            remove => Service.Loaded -= value;
        }

        public event Action SlotsChanged
        {
            add => Service.SlotsChanged += value;
            remove => Service.SlotsChanged -= value;
        }

        public void Register(IGameSerializer serializer) => Service.Register(serializer);
        public void Unregister(IGameSerializer serializer) => Service.Unregister(serializer);
        public void Unregister(string key) => Service.Unregister(key);
        public void Capture() => Service.Capture();
        public SaveOutcome Save(bool onlyIfChanged = false)
        {
            if (Live != this)
                return Live.Save(onlyIfChanged);

            // Everything logged while saving is what could not be saved and why: the Inspector shows it
            var problems = new List<string>();
            void Collect(string condition, string stackTrace, LogType type)
            {
                if (type != LogType.Log && problems.Count < MAX_REPORTED_PROBLEMS)
                    problems.Add(condition);
            }

            SaveLoadService service = Service;
            float started = Time.realtimeSinceStartup;
            SaveOutcome outcome;
            Application.logMessageReceived += Collect;
            try
            {
                outcome = service.Save(onlyIfChanged);
            }
            finally
            {
                Application.logMessageReceived -= Collect;
            }

            if (outcome != SaveOutcome.Unchanged)
            {
                int characters = outcome == SaveOutcome.Saved && _storage.TryRead(service.CurrentSlot, SaveFiles.SAVE, out string text)
                    ? text.Length
                    : 0;
                int objects = _extension is ISaveObjectCount counted ? counted.LastCapturedObjects : 0;
                _lastSave = new SaveReport(outcome, service.CurrentSlot, characters, objects,
                    (Time.realtimeSinceStartup - started) * 1000.0, problems);
            }

            return outcome;
        }

        public LoadOutcome Load()
        {
            if (Live != this)
                return Live.Load();

            // What the game started after the save would land in the loaded game (a reward paid twice): it is dropped,
            // and a save that waited for it is not made over the loaded state
            ForgetWaitingSave();
            WithWork(work => work.Drop());
            return Calmly(_liveLoad ? Service.LoadLive : Service.Load);
        }

        /// <summary>
        /// Loads made in play (buttons, keys, <see cref="Load"/>) keep the scenes loaded: see
        /// <see cref="SaveLoadService.LoadLive"/>. Load On Start is not touched by it.
        /// </summary>
        public bool LiveLoad
        {
            get => Live._liveLoad;
            set => Live._liveLoad = value;
        }

        /// <summary>Loads the current slot keeping the scenes in play: see <see cref="SaveLoadService.LoadInPlace"/>.</summary>
        public LoadOutcome LoadInPlace() => Calmly(Service.LoadInPlace);

        public void SelectSlot(int slot, bool keepData = false) => Service.SelectSlot(slot, keepData);
        public IReadOnlyList<SlotInfo> GetSlots() => Service.GetSlots();
        public SlotInfo GetSlot(int slot) => Service.GetSlot(slot);
        public bool HasSlot(int slot) => Service.HasSlot(slot);
        public bool RenameSlot(int slot, string name) => Service.RenameSlot(slot, name);
        public bool CopySlot(int from, int to) => Service.CopySlot(from, to);
        public int GetLatestSlot() => Service.GetLatestSlot();
        public int GetFreeSlot(int from = 0) => Service.GetFreeSlot(from);
        public void DeleteSlot(int slot) => Service.DeleteSlot(slot);
        public string ExportSlot(int slot) => Service.ExportSlot(slot);
        public void ImportSlot(int slot, string package) => Service.ImportSlot(slot, package);

        // ---------- Buttons: void methods for Button.onClick and other UnityEvents ----------

        /// <summary>Saves the current slot.</summary>
        public void SaveGame() => SaveWhenSettled(onlyIfChanged: false);

        /// <summary>Loads the current slot.</summary>
        public void LoadGame()
        {
            if (!IsBusy(nameof(LoadGame)))
                Load();
        }

        /// <summary>Saves into the slot, keeping everything in play ("save as"); the slot becomes the current one.</summary>
        public void SaveToSlot(int slot)
        {
            if (IsBusy(nameof(SaveToSlot)))
                return;

            if (slot != CurrentSlot)
                SelectSlot(slot, keepData: true);

            SaveWhenSettled(onlyIfChanged: false);
        }

        /// <summary>Makes the slot the current one and loads it.</summary>
        public void LoadFromSlot(int slot)
        {
            if (IsBusy(nameof(LoadFromSlot)))
                return;

            SelectSlot(slot);
            Load();
        }

        /// <summary>A new game in the first free slot: nothing loaded, the new-game scene (or the active one) starts.</summary>
        public void NewGame() => NewGameInSlot(GetFreeSlot());

        /// <summary>A new game in this slot: its file stays until the first save writes over it.</summary>
        public void NewGameInSlot(int slot)
        {
            if (Live != this)
            {
                Live.NewGameInSlot(slot); // its new-game scene and scene loader, not the copy's
                return;
            }

            if (IsBusy(nameof(NewGameInSlot)))
                return;

            SelectSlot(slot);
            LoadNewGameScene();
        }

        /// <summary>Loads the slot saved last, or starts a new game when there is none.</summary>
        public void ContinueGame()
        {
            int latest = GetLatestSlot();
            if (latest < 0)
                NewGame();
            else
                LoadFromSlot(latest);
        }

        /// <summary>Names the current slot; the name is in the slot list from now on.</summary>
        public void RenameCurrentSlot(string slotName) => RenameSlot(CurrentSlot, slotName);

        /// <summary>Deletes every slot: "reset progress".</summary>
        public void DeleteAllSlots()
        {
            if (Live != this)
            {
                Live.DeleteAllSlots();
                return;
            }

            _ = Service;
            foreach (int slot in new List<int>(_storage.ListSlots()))
                DeleteSlot(slot);
        }

        /// <summary>Sends the slot to the server (when one is set); the result comes through Sync.Synced.</summary>
        public void UploadSlot(int slot) => _ = RequireSync(nameof(UploadSlot))?.UploadAsync(slot);

        /// <summary>Takes the slot from the server and loads it, if the server had it.</summary>
        public async void DownloadAndLoadSlot(int slot)
        {
            SaveSync sync = RequireSync(nameof(DownloadAndLoadSlot));
            if (sync != null && await sync.DownloadAsync(slot) == SyncOutcome.Downloaded && this != null)
                LoadFromSlot(slot);
        }

        // ---------- Unity ----------

        private void Awake()
        {
            _untilAutoSave = _autoSavePeriod;
            if (_takingOver)
            {
                // The working system made by a component that sits on an object of the scene: it belongs to no scene
                Current = this;
                DontDestroyOnLoad(gameObject);
                return;
            }

            HomeScene = gameObject.scene;
            Systems.Add(this);
#if UNITY_EDITOR
            // A scene that was never saved with this component (or a component set to Whole Scene by code) has no list
            if (_scope == SaveScope.WholeScene && _sceneObjects.Count == 0)
                EditorBake?.Invoke(this);
#endif
            // A copy too: the list belongs to its scene
            if (_scope == SaveScope.WholeScene)
            {
                CaptureBaselines();
                CaptureSceneObjects();
            }

            if (Current != null && Current != this)
            {
                // A copy (the prefab in the next scene, this scene reloaded by a load): it stays where it is, idle —
                // no load on start, no autosave, no save on quit — and passes its calls and events to Current
                _isIdle = true;
                enabled = false;
                RelayEventsOf(Current);
                return;
            }

            Current = this;
            if (!_keepAcrossScenes || transform.parent != null)
                return; // a child: its root may be kept by the game (a services object) — Start looks

            if (transform.childCount == 0 && GetComponents<Component>().Length == 2)
                DontDestroyOnLoad(gameObject); // an object of its own: just the Transform and this
            else
                HandOverToAnObjectOfItsOwn();
        }

        private static bool _takingOver;
        private bool _handedOver;
        private bool _started;

        /// <summary>
        /// The component was dropped on an object of the scene (the level, a manager, a child): that object must stay
        /// with its scene, and the save system must outlive the scene. So a "SaveSystem (live)" object of its own is
        /// made with the same settings and takes over; this component stays here as a copy that passes calls and
        /// events on, and keeps what belongs to the scene — its scope and object list.
        /// </summary>
        private void HandOverToAnObjectOfItsOwn()
        {
            var holder = new GameObject("SaveSystem (live)");
            holder.SetActive(false); // no Awake until the settings are in
            var live = holder.AddComponent<SaveLoadSystem>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), live);
            // The scene's part stays with the scene's component; the events fire here and are relayed, not twice
            live._scope = SaveScope.Saveables;
            live._sceneObjects = new List<SceneObjectId>();
            live._scenePrefabs = new List<ScenePrefab>();
            live._ignored = new List<GameObject>();
            live._onSaved = new UnityEvent();
            live._onLoaded = new UnityEvent();
            live._onSlotsChanged = new UnityEvent();
            live._onFailed = new UnityEvent<string>();
            live._storage = _storage;
            live._remoteStorage = _remoteStorage;
            live.LoadSceneHandler = LoadSceneHandler;
            live.TickManually = TickManually;

            Current = null;
            _takingOver = true;
            try
            {
                holder.SetActive(true); // its Awake makes it Current
            }
            finally
            {
                _takingOver = false;
            }

            _isIdle = true;
            _handedOver = true;
            enabled = false;
            RelayEventsOf(Current);
        }

        private void Start()
        {
            _started = true;
            // A child of an object that stays with its scene (the game did not keep its root): the scene's next load
            // would take the save system with it
            if (!_isIdle && _keepAcrossScenes && transform.parent != null && gameObject.scene.name != DONT_DESTROY)
            {
                if (_service == null)
                {
                    HandOverToAnObjectOfItsOwn();
                    return; // the live one starts on its own
                }

                Debug.LogWarning("The save system sits on a child of an object that is destroyed when a scene loads, so a " +
                                 "load that reloads the scene cannot finish. Put the component on a root object.", this);
            }

            // The scene this system starts with is the level being entered: it gets its saved state in place, not by
            // loading it a second time (which also drops unsaved scene edits in the editor)
            if (_loadOnStart && !_isIdle)
                Service.LoadAtSceneStart();
        }

        private void Update()
        {
            EndCalmFrames();
            SaveIfSettled();
            if (!TickManually)
                Tick(Time.deltaTime);

            if (!_hotkeys)
                return;

            if (WasPressed(_saveKey))
                SaveGame();
            else if (WasPressed(_loadKey))
                LoadGame();
        }

        /// <summary>The function key went down this frame: the Input System if the project has it, else the old Input.</summary>
        private static bool WasPressed(SaveHotkey key)
        {
            if (key == SaveHotkey.None)
                return false;

            int number = (int)key - 1;
#if ENABLE_INPUT_SYSTEM && SAVE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard[UnityEngine.InputSystem.Key.F1 + number].wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F1 + number);
#else
            return false;
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _saveOnPause && !_isIdle)
                FinishWorkAndSave(onlyIfChanged: true);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && _saveOnPause && !_isIdle)
                SaveWhenSettled(onlyIfChanged: true);
        }

        private void OnApplicationQuit()
        {
            if (_saveOnQuit && !_isIdle)
                FinishWorkAndSave(onlyIfChanged: false);
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;

            Systems.Remove(this);
            if (Systems.Count == 0)
                EndCalmFrames(now: true);
            RelayEventsOf(null);
            _sync?.Dispose();
            _sync = null;
            _extension?.Dispose();
            _extension = null;
        }

#if UNITY_EDITOR
        /// <summary>Set by the editor: finds or creates the settings asset for a system that saves scene objects.</summary>
        public static Action<SaveLoadSystem> EditorValidate;

        /// <summary>Set by the editor: the same right when the component is added, where changing data is allowed.</summary>
        public static Action<SaveLoadSystem> EditorAdded;

        /// <summary>Set by the editor: fills the object and prefab lists of the whole-scene scope.</summary>
        public static Action<SaveLoadSystem> EditorBake;

        public bool SavesSceneObjects => _saveSceneObjects;
        public bool HasSettings => _settings != null;

        private void Reset()
        {
            // Added by hand to a scene: it loads its slot when the scene starts, as the SaveSystem prefab does (a
            // system made by code keeps the field's default and decides itself) — and it is the lazy way in: the whole
            // scene is saved and the keys work, nothing else to set up
            _loadOnStart = true;
            _scope = SaveScope.WholeScene;
            _hotkeys = true;
            EditorAdded?.Invoke(this);
            EditorBake?.Invoke(this);
        }
#endif

        // Instead of [Min]: the old Odin in the project breaks drawing [Min] in Unity 6.5
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_folder))
                _folder = DEFAULT_FOLDER;

            _startupSlot = Mathf.Max(0, _startupSlot);
            _autoSavePeriod = _autoSavePeriod <= 0f ? 0f : Mathf.Max(MIN_AUTO_SAVE_PERIOD, _autoSavePeriod);
#if UNITY_EDITOR
            EditorValidate?.Invoke(this);
#endif
        }

        private SaveLoadService CreateService()
        {
            _storage ??= CreateConfiguredStorage();
            var service = new SaveLoadService(_storage, Application.version)
            {
                SceneName = () => SceneManager.GetActiveScene().name
            };

            int latest = _startInLatestSlot ? service.GetLatestSlot() : -1;
            service.SelectSlot(latest >= 0 ? latest : _startupSlot);
            SaveExtensionAsset settings = _settings != null ? _settings
                : _saveSceneObjects ? FallbackSettings?.Invoke() : null;
            if (settings != null)
            {
                _extension = settings.CreateExtension(service);
                service.Attach(_extension);
            }


            // Something reaches the system before its Start: a DI container that registers while the scene wakes, an
            // Awake, an OnEnable. With Load On Start the slot is read now, so it gets its data the moment it registers
            // and shows the saved game from its first frame; the Load in Start still does the full job
            if (_loadOnStart && !_isIdle && !_started)
                service.Preload();

            service.Saved += RaiseSaved;
            service.Loaded += RaiseLoaded;
            service.SlotsChanged += () => _onSlotsChanged?.Invoke();

            _remoteStorage ??= _remote != null ? _remote.CreateRemote() : null;
            if (_remoteStorage != null)
                _sync = new SaveSync(service, _remoteStorage) { UploadAfterSave = _uploadAfterSave };

            return service;
        }

        /// <summary>
        /// A new storage as the Inspector describes it, wrapped in compression and encryption if they are on: what the
        /// service starts with unless UseStorage replaced it. The editor lists slots through it. The wrapper is always
        /// there: steps that are off are still read (GZip; AES while the key is set), so turning them off does not
        /// make the saves written with them look corrupt.
        /// </summary>
        public ISaveStorage CreateConfiguredStorage()
        {
            ISaveStorage storage = _storageKind switch
            {
                SaveStorageKind.PlayerPrefs => new PlayerPrefsSaveStorage(_folder.TrimEnd('/') + "/"),
                SaveStorageKind.Custom when _customStorage != null => _customStorage.CreateStorage(),
                _ => null
            };

            if (storage == null)
            {
                if (_storageKind == SaveStorageKind.Custom)
                    Debug.LogError("The save system is set to a custom storage but has no SaveStorageAsset: saving to files.",
                        this);

                storage = new FileSaveStorage(Path.Combine(Application.persistentDataPath, _folder));
            }

            var steps = new List<ISaveTransform>();
            if (_compress)
                steps.Add(new GZipSaveTransform());

            bool hasKey = !string.IsNullOrEmpty(_encryptionKey);
            if (_encrypt && !hasKey)
                Debug.LogError("Encryption is on but the key is empty: saves are not encrypted.", this);

            AesSaveTransform aes = hasKey ? new AesSaveTransform(_encryptionKey) : null;
            if (_encrypt && aes != null)
                steps.Add(aes);

            var transformed = new TransformSaveStorage(storage, steps.ToArray());
            if (!_compress)
                transformed.AddReader(new GZipSaveTransform());

            if (!_encrypt && aes != null)
                transformed.AddReader(aes);

            return transformed;
        }

        /// <summary>
        /// An idle copy repeats the UnityEvents of the live system with its own, so a "Saved!" label wired to the
        /// scene's SaveSystem shows; null stops it. Listening to the UnityEvents does not start the live service.
        /// </summary>
        private void RelayEventsOf(SaveLoadSystem live)
        {
            if (!ReferenceEquals(_relayed, null))
            {
                _relayed._onSaved.RemoveListener(_onSaved.Invoke);
                _relayed._onLoaded.RemoveListener(_onLoaded.Invoke);
                _relayed._onSlotsChanged.RemoveListener(_onSlotsChanged.Invoke);
                _relayed._onFailed.RemoveListener(_onFailed.Invoke);
            }

            _relayed = live;
            if (live == null)
                return;

            live._onSaved.AddListener(_onSaved.Invoke);
            live._onLoaded.AddListener(_onLoaded.Invoke);
            live._onSlotsChanged.AddListener(_onSlotsChanged.Invoke);
            live._onFailed.AddListener(_onFailed.Invoke);
        }

        private void RaiseSaved(SaveOutcome outcome)
        {
            if (outcome == SaveOutcome.Saved)
                _onSaved?.Invoke();
            else if (outcome == SaveOutcome.Failed)
                _onFailed?.Invoke($"Saving slot {CurrentSlot} failed.");
        }

        // ---------- The long frame of a load ----------

        // Loading a scene takes a frame many times longer than the others, and Unity hands that time to the next
        // frame as its deltaTime (up to Time.maximumDeltaTime): everything that was saved in motion would jump —
        // a bullet at 300 m/s flies 30 metres in one step and is gone before it is seen. The game did not play
        // while it was loading, so that frame gets an ordinary step instead.
        // The long step goes to the frame in which the scene arrives — its deltaTime is set before anything of the
        // load runs — and Unity repeats it in the frame after. So the step is capped from the moment a load is asked
        // for until a couple of frames after it has ended. One cap for all the systems: there is one clock.
        private const int CALM_FRAMES_AFTER = 2;
        private static float _usualMaximumDeltaTime;
        private static bool _calm;
        private static int _calmUntilFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCalm() => _calm = false;

        private static void BeginCalmFrames()
        {
            if (!_calm)
            {
                _usualMaximumDeltaTime = Time.maximumDeltaTime;
                _calm = true;
            }

            _calmUntilFrame = int.MaxValue;
            // Unity does not take less than the fixed step
            Time.maximumDeltaTime = Mathf.Min(_usualMaximumDeltaTime, Time.fixedDeltaTime);
        }

        /// <summary>The load has ended (or never started): the cap stays for the frames that still carry its time.</summary>
        private static void CalmFramesEndSoon()
        {
            if (_calm)
                _calmUntilFrame = Time.frameCount + CALM_FRAMES_AFTER;
        }

        private static void EndCalmFrames(bool now = false)
        {
            if (!_calm || (!now && Time.frameCount < _calmUntilFrame))
                return;

            _calm = false;
            Time.maximumDeltaTime = _usualMaximumDeltaTime;
        }

        private LoadOutcome Calmly(Func<LoadOutcome> load)
        {
            BeginCalmFrames();
            LoadOutcome outcome;
            try
            {
                outcome = load();
            }
            catch
            {
                CalmFramesEndSoon();
                throw;
            }

            // Refused, empty or applied in place at once: nothing is on its way any more
            if (!Service.IsLoading)
                CalmFramesEndSoon();

            return outcome;
        }

        private void RaiseLoaded(LoadOutcome outcome)
        {
            CalmFramesEndSoon();
            switch (outcome)
            {
                case LoadOutcome.Loaded:
                case LoadOutcome.Recovered:
                case LoadOutcome.Empty:
                case LoadOutcome.Corrupted:
                    _onLoaded?.Invoke();
                    break;
                default:
                    _onFailed?.Invoke($"Slot {CurrentSlot} was not loaded: {outcome}.");
                    break;
            }
        }

        private void LoadNewGameScene()
        {
            string scene = string.IsNullOrEmpty(_newGameScene) ? SceneManager.GetActiveScene().path : _newGameScene;
            if (LoadSceneHandler != null)
            {
                LoadSceneHandler(scene);
                return;
            }

#if UNITY_EDITOR
            // A scene that had unsaved changes when Play started is played from its copy, not from the file on disk
            scene = SaveSceneCopies.ToLoad(scene);
            // A scene outside Build Settings (a sandbox) loads in the editor only
            if (scene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && SceneUtility.GetBuildIndexByScenePath(scene) < 0)
            {
                EditorSceneManager.LoadSceneInPlayMode(scene, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene);
        }

        private bool IsBusy(string method)
        {
            if (!IsLoading)
                return false;

            Debug.LogWarning($"{method} is ignored: a load is in progress.", this);
            return true;
        }

        private SaveSync RequireSync(string method)
        {
            SaveSync sync = Sync;
            if (sync == null)
                Debug.LogWarning($"{method} needs a server: set a RemoteSaveStorageAsset on the save system.", this);

            return sync;
        }

        private void EnsureNotStarted(string method)
        {
            if (_service != null)
                throw new InvalidOperationException($"{method} must be called before the save system starts.");
        }
    }
}
