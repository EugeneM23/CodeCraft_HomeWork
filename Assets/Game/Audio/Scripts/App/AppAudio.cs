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
    /// Music: the menu and hangar catalogs loop in order, the battle catalog plays in random order; switching fades
    /// the current track out and the next one in. Tracks are advanced here, not by MusicPlaylist: MusicPlayer
    /// notices a finished track by AudioSource.time reaching the clip length, which a stopped source never reports.
    ///
    /// Battle: when any of the three layer catalogs has a clip, the battle plays <see cref="LayeredMusic"/> instead
    /// of the battle catalog, its layers following <see cref="MusicIntensity"/> (set by the horde). An elite
    /// crossfades to the elite catalog and back; without elite music it pushes the layers to their peak.
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
        [Tooltip("Main menu: loops in order.")]
        [SerializeField] private MusicCatalog _menuMusic;
        [Tooltip("Hangar and chapter choice: loops in order. Empty plays the menu music.")]
        [SerializeField] private MusicCatalog _hangarMusic;
        [Tooltip("Battle without layers: random order.")]
        [SerializeField] private MusicCatalog _battleMusic;
        [Tooltip("While an elite is alive: loops in order. Empty pushes the battle layers to their peak instead.")]
        [SerializeField] private MusicCatalog _eliteMusic;
        [Tooltip("Seconds the current track takes to fade out, and the next to fade in.")]
        [SerializeField, Min(0f)] private float _musicFade = 1f;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 1f;

        [Header("Battle layers")]
        [Tooltip("Plays through the whole battle. One clip per layer, all three of one length.")]
        [SerializeField] private MusicCatalog _battleCalm;
        [Tooltip("Rises with the horde: intensity 0.15 to 0.5.")]
        [SerializeField] private MusicCatalog _battleHorde;
        [Tooltip("Rises at the peak: intensity 0.5 to 0.9.")]
        [SerializeField] private MusicCatalog _battlePeak;
        [Tooltip("Looping sources of the three layers. Routed to the Music group.")]
        [SerializeField] private AudioSource _calmSource;
        [SerializeField] private AudioSource _hordeSource;
        [SerializeField] private AudioSource _peakSource;

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

        private readonly MusicIntensity _intensity = new MusicIntensity();
        private LayeredMusic _layers;
        private bool _battle;
        private bool _elite;

        public static AppAudio Instance { get; private set; }

        public IAudioVolumes Volumes => _volumes;

        public IUiSounds UiSounds => _uiSounds;

        /// <summary>The battle intensity, 0..1, the layers follow.</summary>
        public float BattleIntensity => _intensity.Value;

        public void PlayMenuMusic()
        {
            LeaveBattle();
            RequestMusic(_menuMusic, false);
        }

        /// <summary>Hangar and chapter choice; the menu music while the hangar catalog is empty.</summary>
        public void PlayHangarMusic()
        {
            LeaveBattle();
            RequestMusic(HasClips(_hangarMusic) ? _hangarMusic : _menuMusic, false);
        }

        /// <summary>The layers when they have clips, else the battle catalog. A new battle starts calm.</summary>
        public void PlayBattleMusic()
        {
            if (!_battle)
                _intensity.Reset();
            _battle = true;
            ApplyBattleMusic();
        }

        public void StopMusic()
        {
            LeaveBattle();
            RequestMusic(null, false);
        }

        /// <summary>All live horde monsters against the arena's limit, 0..1; smoothed before the layers hear it.</summary>
        public void SetBattleIntensity(float total01) => _intensity.SetTarget(total01);

        /// <summary>A wave was released: the intensity jumps up for a few seconds.</summary>
        public void SurgeBattleIntensity() => _intensity.Surge();

        /// <summary>
        /// An elite is alive: crossfades to the elite music, or back to the battle music on false. Without elite
        /// music the battle intensity is held at 1 instead. Cleared by the menu, the hangar and StopMusic.
        /// </summary>
        public void SetEliteMusic(bool on)
        {
            if (_elite == on)
                return;

            _elite = on;
            if (_battle)
                ApplyBattleMusic();
        }

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
            _layers = new LayeredMusic(_musicGroup,
                _calmSource, _battleCalm, _hordeSource, _battleHorde, _peakSource, _battlePeak);

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

        private void Update() // perf-ok: ticks during pause (music fades, track changes, battle layers)
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (_layers != null)
            {
                _intensity.Tick(deltaTime);
                _layers.Tick(deltaTime, _musicVolume, _intensity.Value);
            }

            if (_musicPlayer == null)
                return;

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

        // Which battle music plays: the elite catalog, the layers or the plain battle catalog. The layers and the
        // music player have their own sources, so going between them is a crossfade.
        private void ApplyBattleMusic()
        {
            bool eliteCatalog = _elite && HasClips(_eliteMusic);
            _intensity.Forced = _elite && !eliteCatalog;

            if (eliteCatalog)
            {
                _layers?.Stop(_musicFade);
                RequestMusic(_eliteMusic, false);
            }
            else if (_layers != null && _layers.HasClips)
            {
                RequestMusic(null, false);
                _layers.Play(_musicFade);
            }
            else
            {
                RequestMusic(_battleMusic, true);
            }
        }

        private void LeaveBattle()
        {
            _battle = false;
            _elite = false;
            _intensity.Forced = false;
            _layers?.Stop(_musicFade);
        }

        private static bool HasClips(MusicCatalog catalog) => catalog != null && catalog.Count > 0;

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
