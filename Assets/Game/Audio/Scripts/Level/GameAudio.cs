using System;
using AudioEngine;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The sounds of one level: the module's AudioSystem on the same object with the level's banks. Freezes them
    /// all while the battle is paused, preloads their clips, starts the battle music, and hooks the level up:
    /// entity sounds (<see cref="EntityAudioDirector"/>), ambience (<see cref="WorldAmbienceAudioBehaviour"/>),
    /// gameplay signals (<see cref="GameSignalAudio"/>).
    ///
    /// Gameplay calls the static API; every call is a no-op while there is no GameAudio. Ids are
    /// <see cref="AudioKeys"/> constants; the repeat limit comes from <see cref="AudioLayout"/>.
    ///
    /// Until the core builds levels from scenes, <see cref="Bootstrap"/> makes one from Resources/GameAudio in
    /// every loaded scene that has a GameContext.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSystem))]
    public sealed class GameAudio : MonoBehaviour
    {
        private const string RESOURCE = "GameAudio";

        private static readonly HordeAudio s_horde = new();
        private static readonly EliteAudio s_elite = new();
        private static readonly RunAudio s_run = new();
        private static GameAudio s_instance;
        private static GameAudio s_prefab;
        private static bool s_missingPrefabWarned;

        [Tooltip("Every clip the banks of this level use. Filled by Tools/Audio/Sync Banks; loaded on start so no sound loads from disk mid-fight.")]
        [SerializeField] private AudioClip[] _preloadClips = Array.Empty<AudioClip>();
        [Tooltip("Starts the battle music of AppAudio when the level starts.")]
        [SerializeField] private bool _playBattleMusic = true;

        private AudioSystem _system;
        private IAudioPauseSource _pauseSource;
        private bool _paused;
        private GameSignalAudio _signals;
        private EntityAudioDirector _director;
        private GameContext _context;
        private WorldAmbienceAudioBehaviour _ambience;
        private RunEventAudio _runEvents;

        /// <summary>The level's GameAudio, null when there is none.</summary>
        public static GameAudio Instance => s_instance;

        /// <summary>There is a GameAudio and it takes sounds.</summary>
        public static bool IsReady => s_instance != null && s_instance.isActiveAndEnabled;

        /// <summary>Horde sounds for the AI team: crowd, pack, waves, burrows, bites, spits, deaths.</summary>
        public static HordeAudio Horde => s_horde;

        /// <summary>The elite Regax's sounds for the AI team.</summary>
        public static EliteAudio Elite => s_elite;

        /// <summary>Sounds of the run: arenas, gates, experience, levels, skills, crushes.</summary>
        public static RunAudio Run => s_run;

        public AudioSystem System => _system;

        public bool IsPaused => _paused;

        /// <summary>For the editor sync: the clips to preload.</summary>
        public AudioClip[] PreloadClips
        {
            get => _preloadClips;
            set => _preloadClips = value ?? Array.Empty<AudioClip>();
        }

        #region Static API

        /// <summary>A 2D sound, or a 3D one at the listener-independent origin.</summary>
        public static bool Play(string id)
        {
            if (!CanPlay())
                return false;

            return s_instance._system.PlayEvent(id, Vector3.zero, AudioLayout.MaxFrequency(id));
        }

        /// <summary>A one-shot at a point.</summary>
        public static bool Play(string id, Vector3 position)
        {
            if (!CanPlay())
                return false;

            return s_instance._system.PlayEvent(id, position, AudioLayout.MaxFrequency(id));
        }

        /// <summary>A one-shot that follows a transform, which must outlive it.</summary>
        public static bool Play(string id, Transform follow)
        {
            if (follow == null)
                return false;
            if (!CanPlay())
                return false;

            return s_instance._system.PlayEvent(id, follow, AudioLayout.MaxFrequency(id));
        }

        /// <summary>Starts a sound (a loop as a rule) and hands its handle back; 2D or at the origin.</summary>
        public static bool StartLoop(string id, out AudioEventHandle handle)
        {
            if (!CanPlay())
            {
                handle = default;
                return false;
            }

            return s_instance._system.PlayEvent(id, Vector3.zero, out handle, AudioLayout.MaxFrequency(id));
        }

        /// <summary>Starts a sound (a loop as a rule) at a point and hands its handle back; move it with SetPosition.</summary>
        public static bool StartLoop(string id, Vector3 position, out AudioEventHandle handle)
        {
            if (!CanPlay())
            {
                handle = default;
                return false;
            }

            return s_instance._system.PlayEvent(id, position, out handle, AudioLayout.MaxFrequency(id));
        }

        /// <summary>
        /// Starts a sound (a loop as a rule) that follows a transform and hands its handle back. The transform must
        /// outlive the sound: stop it before the object is destroyed.
        /// </summary>
        public static bool StartLoop(string id, Transform follow, out AudioEventHandle handle)
        {
            if (follow == null || !CanPlay())
            {
                handle = default;
                return false;
            }

            return s_instance._system.PlayEvent(id, follow, out handle, AudioLayout.MaxFrequency(id));
        }

        /// <summary>
        /// Fades a started sound out and releases it; clears the handle. Does nothing once the level is going away:
        /// the AudioSystem takes its sounds with it.
        /// </summary>
        public static void StopLoop(ref AudioEventHandle handle, float fadeOut = 0.3f)
        {
            if (IsReady && handle.IsValid)
            {
                if (fadeOut > 0f)
                    handle.Dispose(fadeOut);
                else
                    handle.Dispose();
            }

            handle = default;
        }

        /// <summary>Sets a float bank parameter, e.g. <see cref="AudioKeys.Tank.RPM"/>.</summary>
        public static void SetParameter(string id, float value)
        {
            if (s_instance != null)
                s_instance._system.SetFloat(id, value);
        }

        private static bool CanPlay() => IsReady && !s_instance._paused;

        #endregion

        /// <summary>Freezes (or resumes) every sound of the level. Only acts on a change.</summary>
        public void SetPaused(bool paused)
        {
            if (paused == _paused || _system == null)
                return;

            _paused = paused;
            if (paused)
                _system.Pause();
            else
                _system.Resume();
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                // Destroy is deferred: a duplicate would otherwise still get Update before it goes.
                enabled = false;
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            _system = GetComponent<AudioSystem>();
            _pauseSource = CreatePauseSource();
            _signals = new GameSignalAudio();
            Preload();
        }

        private void OnEnable()
        {
            if (s_instance == this)
                _signals?.Subscribe();
        }

        private void OnDisable()
        {
            if (s_instance == this)
                _signals?.Unsubscribe();
        }

        private void Start()
        {
            if (s_instance != this)
                return;

            if (_playBattleMusic && AppAudio.Instance != null)
                AppAudio.Instance.PlayBattleMusic();

            if (!GameContext.TryGetInstance(out _context))
                return;

            if (_context.TryGetEntityWorld(out IEntityWorld<IEntity> world) && world != null)
                _director = new EntityAudioDirector(world);

            _ambience = new WorldAmbienceAudioBehaviour();
            _context.AddBehaviour(_ambience);

            // The run and the deaths are absent in the sandbox: whatever the level has is listened to.
            _context.TryGetRun(out IRunService run);
            _context.TryGetEnemyDeaths(out IEnemyDeaths deaths);
            if (run != null || deaths != null)
                _runEvents = new RunEventAudio(run, deaths);
        }

        // Polls the pause source while the game is paused: Atomic ticks may stop with the pause, this must not.
        private void Update() // perf-ok: ticks during pause (pause poll, delayed signal sounds)
        {
            if (s_instance != this)
                return;

            SetPaused(_pauseSource.IsPaused);
            if (!_paused)
                _signals.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (s_instance != this)
                return;

            _director?.Dispose();
            _director = null;

            _runEvents?.Dispose();
            _runEvents = null;

            if (_context != null && _ambience != null)
                _context.DelBehaviour(_ambience);
            _ambience = null;

            // The battle music this level started stops with it (AppAudio starts menu music in a menu scene).
            AppAudio app = AppAudio.Instance;
            if (_playBattleMusic && app != null)
                app.StopMusic();

            (_pauseSource as IDisposable)?.Dispose();
            _pauseSource = null;

            ResetFacades();
            s_instance = null;
        }

        // The core's game loop when the app is up; a sandbox Play without the app falls back to Time.timeScale.
        private static IAudioPauseSource CreatePauseSource()
        {
            if (AppContext.TryGet(out AppContext app) && app.TryGetGameLoop(out Ticking.IGameLoop loop) && loop != null)
                return new GameLoopPauseSource(loop);

            return new TimeScalePauseSource();
        }

        private void Preload()
        {
            for (int i = 0; i < _preloadClips.Length; i++)
            {
                AudioClip clip = _preloadClips[i];
                if (clip != null && clip.loadState is AudioDataLoadState.Unloaded or AudioDataLoadState.Failed)
                    clip.LoadAudioData();
            }
        }

        #region Bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            s_instance = null;
            s_prefab = null;
            s_missingPrefabWarned = false;
            ResetFacades();
        }

        private static void ResetFacades()
        {
            s_horde.Reset();
            s_elite.Reset();
            s_run.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForLevel();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureForLevel();

        private static void EnsureForLevel()
        {
            if (s_instance != null || !GameContext.TryGetInstance(out _))
                return;

            if (s_prefab == null)
                s_prefab = Resources.Load<GameAudio>(RESOURCE);

            if (s_prefab == null)
            {
                if (!s_missingPrefabWarned)
                    Debug.LogWarning($"[Audio] No Resources/{RESOURCE} prefab: the level plays no world sounds.");
                s_missingPrefabWarned = true;
                return;
            }

            Instantiate(s_prefab).name = RESOURCE;
        }

        #endregion
    }
}
