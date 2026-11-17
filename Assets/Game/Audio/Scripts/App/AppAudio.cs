using System.Collections.Generic;
using AudioEngine;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Audio that lives for the whole game and ignores the battle pause: interface sounds, music, channel volumes.
    /// One per game, DontDestroyOnLoad. Made by <see cref="AppAudioInstaller"/> on the App prefab, so it lives exactly
    /// as long as the app: a Play without the app (tests, a bare scene) has no AppAudio and no music.
    ///
    /// Music: the menu catalog loops in order, the battle catalog plays in random order; switching fades the
    /// current track out and the next one in. Tracks are advanced here, not by MusicPlaylist: MusicPlayer
    /// notices a finished track by AudioSource.time reaching the clip length, which a stopped source never reports.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppAudio : MonoBehaviour
    {
        /// <summary>Seconds a track must have played before a silent source counts as finished, not still starting.</summary>
        private const float TRACK_START_GRACE = 0.5f;

        private enum MusicFade { None, Out, In }

        [SerializeField] private AudioMixer _mixer;

        [Header("UI")]
        [SerializeField] private UISoundPlayer _uiSoundPlayer;
        [SerializeField] private UISoundCatalog _uiCatalog;
        [Tooltip("The source of the UISoundPlayer. Routed to the UI group and kept playing through AudioListener.pause.")]
        [SerializeField] private AudioSource _uiSource;
        [SerializeField] private AudioMixerGroup _uiGroup;

        [Header("Music")]
        [SerializeField] private MusicPlayer _musicPlayer;
        [Tooltip("The source of the MusicPlayer. Routed to the Music group.")]
        [SerializeField] private AudioSource _musicSource;
        [Tooltip("Plays the victory and defeat stingers. Routed to the Music group; empty plays them as UI sounds.")]
        [SerializeField] private AudioSource _stingerSource;
        [SerializeField] private AudioMixerGroup _musicGroup;
        [Tooltip("Main menu and hangar: loops in order.")]
        [SerializeField] private MusicCatalog _menuMusic;
        [Tooltip("Battle: random order.")]
        [SerializeField] private MusicCatalog _battleMusic;
        [Tooltip("Seconds the current track takes to fade out, and the next to fade in.")]
        [SerializeField, Min(0f)] private float _musicFade = 1f;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 1f;

        private GameAudioVolumes _volumes;
        private AudioSettingsBridge _settings;
        private GameUiSounds _uiSounds;

        private MusicCatalog _currentCatalog;
        private MusicCatalog _nextCatalog;
        private bool _nextShuffled;
        private bool _hasNext;
        private IEnumerator<AudioClip> _tracks;
        private MusicFade _fade;
        private float _fadeLevel;
        private float _trackTime;

        public static AppAudio Instance { get; private set; }

        public IAudioVolumes Volumes => _volumes;

        public IUiSounds UiSounds => _uiSounds;

        public void PlayMenuMusic() => RequestMusic(_menuMusic, false);

        public void PlayBattleMusic() => RequestMusic(_battleMusic, true);

        public void StopMusic() => RequestMusic(null, false);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Route(_uiSource, _uiGroup);
            Route(_musicSource, _musicGroup);
            Route(_stingerSource, _musicGroup);

            if (_uiSoundPlayer != null && _uiCatalog != null)
                _uiSoundPlayer.SetCatalog(_uiCatalog);

            _volumes = new GameAudioVolumes(_mixer);
            _uiSounds = new GameUiSounds(_uiSoundPlayer, _uiCatalog, _stingerSource);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // AudioMixer.SetFloat is ignored in Awake.
        private void Start()
        {
            _volumes?.ApplyAll();
            TryBindSettings();
        }

        // The app (and its settings) may come up after AppAudio: try again on each scene load until bound.
        private void TryBindSettings()
        {
            if (_settings != null || _volumes == null)
                return;

            if (AppContext.TryGet(out AppContext app) && app.TryGetSettings(out SettingsStore store) && store != null)
                _settings = new AudioSettingsBridge(store, _volumes);
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            _settings?.Dispose();
            _settings = null;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        // A scene without a GameContext is a menu: it gets the menu music. Battle music is started by GameAudio.
        // One lookup per scene load, not per frame.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBindSettings();
            if (!GameContext.TryGetInstance(out _))
                PlayMenuMusic();
        }

        private void Update() // perf-ok: ticks during pause (music fades and track changes)
        {
            if (_musicPlayer == null)
                return;

            float deltaTime = Time.unscaledDeltaTime;
            float step = _musicFade > 0f ? deltaTime / _musicFade : 1f;

            switch (_fade)
            {
                case MusicFade.Out:
                    _fadeLevel = Mathf.Max(0f, _fadeLevel - step);
                    if (_fadeLevel <= 0f || _currentCatalog == null)
                        SwitchToNext();
                    break;
                case MusicFade.In:
                    _fadeLevel = Mathf.Min(1f, _fadeLevel + step);
                    if (_fadeLevel >= 1f)
                        _fade = MusicFade.None;
                    break;
            }

            _musicPlayer.Volume = _musicVolume * _fadeLevel;

            if (_currentCatalog != null && _fade != MusicFade.Out)
                AdvanceTrackIfFinished(deltaTime);
        }

        private void RequestMusic(MusicCatalog catalog, bool shuffled)
        {
            if (catalog != null && catalog.Count == 0)
                catalog = null;

            bool same = _hasNext ? catalog == _nextCatalog : catalog == _currentCatalog;
            if (same)
                return;

            _nextCatalog = catalog;
            _nextShuffled = shuffled;
            _hasNext = true;
            _fade = MusicFade.Out;
        }

        private void SwitchToNext()
        {
            StopTrack();
            _currentCatalog = _nextCatalog;
            _hasNext = false;
            _nextCatalog = null;
            _tracks = null;

            if (_currentCatalog == null)
            {
                _fade = MusicFade.None;
                _fadeLevel = 0f;
                return;
            }

            // One enumerator per catalog switch, not per frame.
            _tracks = _nextShuffled
                ? new RandomTrackEnumerator(_currentCatalog)
                : new SequenceTrackEnumerator(_currentCatalog, true, false);
            _fadeLevel = 0f;
            _fade = MusicFade.In;
            PlayNextTrack();
        }

        private void AdvanceTrackIfFinished(float deltaTime)
        {
            _trackTime += deltaTime;
            MusicPlayer.State state = _musicPlayer.CurrentState;
            bool finished = state == MusicPlayer.State.FINISHED ||
                            state == MusicPlayer.State.PLAYING && _trackTime > TRACK_START_GRACE &&
                            _musicSource != null && !_musicSource.isPlaying;
            if (!finished)
                return;

            StopTrack();
            PlayNextTrack();
        }

        private void PlayNextTrack()
        {
            _trackTime = 0f;
            if (_tracks != null && _tracks.MoveNext() && _tracks.Current != null)
                _musicPlayer.Play(_tracks.Current);
        }

        private void StopTrack()
        {
            if (_musicPlayer.CurrentState != MusicPlayer.State.IDLE)
                _musicPlayer.Stop();
        }

        private static void Route(AudioSource source, AudioMixerGroup group)
        {
            if (source == null)
                return;

            if (group != null)
                source.outputAudioMixerGroup = group;
            source.ignoreListenerPause = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
