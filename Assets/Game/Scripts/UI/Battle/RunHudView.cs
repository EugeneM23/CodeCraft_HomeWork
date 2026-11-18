using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using Game.Arenas;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The run's part of the HUD (GDD §11), read from IRunHudSource: arena n/5, time, level and experience bar,
    /// marks and samples of the run, skill icons (a pool of 10 places: up to 4 weapons and 6 upgrades), the
    /// special module (E) with its recharge, the elite's health bar, the gate pointer and «Давка ×N»; when an
    /// arena starts, its hints (ArenaStarted for the key hints, a panel of its own for the rest, ~8 s).
    /// The gate pointer is computed from the tank (PlayerContext.Character), never from the world origin.
    /// Texts are rewritten on Changed or at most 4 times a second, numbers through SetText; the bars, the
    /// pointer and the fade are moved per frame from cached fields. No allocations per frame.
    /// </summary>
    public sealed class RunHudView : MonoBehaviour
    {
        private const float REFRESH_INTERVAL = 0.25f;
        private const float POINTER_MARGIN = 72f;
        private const float MARKER_LIFT = 48f;
        private const float CRUSH_FADE = 0.6f;
        private const int CRUSH_SHOWN_FROM = 2;
        private const float FILL_EPSILON = 0.002f;
        private const float DIRECTION_PROBE = 5f;
        private const float ARENA_HINT_SHOWN_FOR = 8f;
        private const float ARENA_HINT_FADE = 1f;

        /// <summary>A new arena began with these hints (HudScreen shows the key hints for Controls).</summary>
        public event Action<ArenaHint> ArenaStarted;

        [Header("Counters")]
        [SerializeField] private TMP_Text _arenaText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private TMP_Text _marksText;
        [SerializeField] private TMP_Text _samplesText;

        [Header("Experience")]
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private Image _xpFill;
        [SerializeField] private TMP_Text _xpText;

        [Header("Skills")]
        [Tooltip("Pooled places, weapons first: 10 for 4 weapons and 6 upgrades")]
        [SerializeField] private SkillIconView[] _skillIcons;

        [Header("Special (E)")]
        [SerializeField] private GameObject _specialGroup;
        [SerializeField] private Image _specialFill;
        [SerializeField] private Image _specialFrame;

        [Header("Elite")]
        [SerializeField] private GameObject _eliteGroup;
        [SerializeField] private Image _eliteFill;

        [Header("Gate")]
        [Tooltip("Full-screen rect the pointer moves in")]
        [SerializeField] private RectTransform _pointerArea;
        [SerializeField] private RectTransform _gatePointer;
        [Tooltip("The arrow (points down unrotated), turned towards the gate")]
        [SerializeField] private RectTransform _gateArrow;

        [Header("Crush streak")]
        [SerializeField] private CanvasGroup _crushGroup;
        [SerializeField] private TMP_Text _crushText;

        [Header("Arena hints (crush, destroy cover)")]
        [SerializeField] private CanvasGroup _arenaHintGroup;
        [SerializeField] private TMP_Text _arenaHintText;

        private IRunHudSource _source;
        private Action _onChanged;
        private Camera _camera;
        private Transform _tank;
        private Subscription<IEntity> _characterSubscription;
        private Action<IEntity> _onCharacterChanged;
        private float _arenaHintShownAt = -1f;

        private bool _dirty;
        private float _nextRefresh;
        private int _shownArena = -1;
        private int _shownArenaCount = -1;
        private int _shownSeconds = -1;
        private int _shownMarks = -1;
        private int _shownSamples = -1;
        private int _shownLevel = -1;
        private int _shownXp = -1;
        private int _shownXpNeed = -1;
        private int _shownSkillCount = -1;
        private int _shownCrush = -1;
        private float _crushEndedAt = -1f;
        private bool _specialReady;
        private bool _specialKnown;

        private string _arenaFormat;
        private string _levelFormat;
        private string _xpFormat;
        private string _marksFormat;
        private string _samplesFormat;
        private string _timeFormat;
        private string _crushFormat;

        private void Awake()
        {
            _arenaFormat = UiStrings.Get("run.hud_arena_format");
            _levelFormat = UiStrings.Get("run.hud_level_format");
            _xpFormat = UiStrings.Get("run.hud_xp_format");
            _marksFormat = UiStrings.Get("run.hud_marks_format");
            _samplesFormat = UiStrings.Get("run.hud_samples_format");
            _timeFormat = UiStrings.Get("run.hud_time_format");
            _crushFormat = UiStrings.Get("run.hud_crush_format");

            _eliteGroup.SetActive(false);
            _gatePointer.gameObject.SetActive(false);
            _crushGroup.alpha = 0f;
            _arenaHintGroup.gameObject.SetActive(false);
            for (int i = 0; i < _skillIcons.Length; i++)
                _skillIcons[i].Hide();
            _onCharacterChanged = HandleCharacterChanged;
        }

        private void OnEnable()
        {
            if (GameContext.TryGetInstance(out GameContext context))
                _characterSubscription = context.GetPlayerContext().GetCharacter().Observe(_onCharacterChanged);
        }

        private void OnDisable()
        {
            _characterSubscription.Dispose();
            _characterSubscription = default;
        }

        // The tank or the character on foot; the last one is kept while there is none.
        private void HandleCharacterChanged(IEntity character)
        {
            if (character == null)
                return;
            _tank = character is Component component ? component.transform
                : character.TryGetTransform(out Transform transform) ? transform : _tank;
        }

        /// <summary>The run to show; null shows nothing of it. May come before Awake (the HUD is off until shown).</summary>
        public void Bind(IRunHudSource source)
        {
            _onChanged ??= HandleChanged;
            if (_source != null)
                _source.Changed -= _onChanged;

            _source = source;
            if (_source != null)
                _source.Changed += _onChanged;

            _shownArena = _shownArenaCount = _shownSeconds = _shownMarks = _shownSamples = -1;
            _shownLevel = _shownXp = _shownXpNeed = _shownSkillCount = _shownCrush = -1;
            _specialKnown = false;
            _dirty = true;
        }

        private void OnDestroy()
        {
            if (_source != null)
                _source.Changed -= _onChanged;
        }

        private void HandleChanged() => _dirty = true;

        /// <summary>Per frame from HudScreen (unscaled time): bars, pointer and fade; texts on change or 4 times a second.</summary>
        public void Tick(float now)
        {
            if (_source == null)
                return;

            TickSpecial();
            TickElite();
            TickGate();
            TickCrush(now);
            TickArenaHint(now);

            if (!_dirty && now < _nextRefresh)
                return;

            _dirty = false;
            _nextRefresh = now + REFRESH_INTERVAL;
            RefreshCounters(now);
            RefreshSkills();
        }

        // ---------- Texts ----------

        private void RefreshCounters(float now)
        {
            int arena = _source.ArenaIndex;
            int arenaCount = _source.ArenaCount;
            if (arena != _shownArena || arenaCount != _shownArenaCount)
            {
                bool started = arena != _shownArena;
                _shownArena = arena;
                _shownArenaCount = arenaCount;
                _arenaText.SetText(_arenaFormat, arena, arenaCount);
                if (started)
                    StartArenaHints(_source.ArenaHints, now);
            }

            int seconds = ClockTextUseCase.ShownSeconds(_source.ElapsedTime);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                ClockTextUseCase.Split(seconds, out int minutes, out int rest);
                _timeText.SetText(_timeFormat, minutes, rest);
            }

            int marks = _source.Marks;
            if (marks != _shownMarks)
            {
                _shownMarks = marks;
                _marksText.SetText(_marksFormat, marks);
            }

            int samples = _source.Samples;
            if (samples != _shownSamples)
            {
                _shownSamples = samples;
                _samplesText.SetText(_samplesFormat, samples);
            }

            int level = _source.Level;
            if (level != _shownLevel)
            {
                _shownLevel = level;
                _levelText.SetText(_levelFormat, level);
            }

            int xp = _source.Xp;
            int need = _source.XpToNextLevel;
            if (xp != _shownXp || need != _shownXpNeed)
            {
                _shownXp = xp;
                _shownXpNeed = need;
                _xpFill.fillAmount = need > 0 ? Mathf.Clamp01((float)xp / need) : 0f;
                _xpText.SetText(_xpFormat, xp, need);
            }
        }

        private void RefreshSkills()
        {
            IReadOnlyList<RunSkill> skills = _source.Skills;
            int count = skills != null ? Mathf.Min(skills.Count, _skillIcons.Length) : 0;
            for (int i = 0; i < count; i++)
                _skillIcons[i].Show(skills[i]); // rewrites only what changed

            if (count == _shownSkillCount)
                return;

            _shownSkillCount = count;
            for (int i = count; i < _skillIcons.Length; i++)
                _skillIcons[i].Hide();
        }

        // ---------- Per frame ----------

        private void TickSpecial()
        {
            float special = _source.Special01;
            bool has = special >= 0f;
            if (_specialGroup.activeSelf != has)
                _specialGroup.SetActive(has);
            if (!has)
                return;

            SetFill(_specialFill, special);
            bool ready = special >= 1f;
            if (_specialKnown && ready == _specialReady)
                return;

            _specialKnown = true;
            _specialReady = ready;
            _specialFill.color = ready ? UiStyle.Accent : UiStyle.Disabled;
            _specialFrame.color = ready ? UiStyle.Accent : UiStyle.Panel;
        }

        private void TickElite()
        {
            bool alive = _source.EliteAlive;
            if (_eliteGroup.activeSelf != alive)
                _eliteGroup.SetActive(alive);
            if (alive)
                SetFill(_eliteFill, Mathf.Clamp01(_source.EliteHealth01));
        }

        private void TickGate()
        {
            bool open = _source.GateOpen;
            if (open && _camera == null)
                _camera = Camera.main; // perf-ok: cached, looked up again only when the camera is gone
            bool show = open && _camera != null && _tank != null;
            if (_gatePointer.gameObject.activeSelf != show)
                _gatePointer.gameObject.SetActive(show);
            if (!show)
                return;

            // Direction from the tank, flat, shown on screen by projecting a point a few metres ahead of the tank.
            Vector3 gate = _source.GatePosition;
            Vector3 tank = _tank.position;
            Vector3 toGate = gate - tank;
            toGate.y = 0f;
            Vector3 tankScreen = _camera.WorldToScreenPoint(tank);
            Vector2 direction = Vector2.zero;
            if (toGate.sqrMagnitude > 0.0001f)
            {
                Vector3 aheadScreen = _camera.WorldToScreenPoint(tank + toGate.normalized * DIRECTION_PROBE);
                direction = new Vector2(aheadScreen.x - tankScreen.x, aheadScreen.y - tankScreen.y);
            }

            Vector3 gateScreen = _camera.WorldToScreenPoint(gate);
            var screenSize = new Vector2(Screen.width, Screen.height);
            float scale = _pointerArea.rect.width > 0f ? Screen.width / _pointerArea.rect.width : 1f;
            bool onScreen = GatePointerUseCase.Place(gateScreen, new Vector2(tankScreen.x, tankScreen.y), direction, screenSize,
                POINTER_MARGIN * scale, out Vector2 position, out float angle);
            if (onScreen)
                position.y += MARKER_LIFT * scale;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_pointerArea, position, null, out Vector2 local))
                _gatePointer.anchoredPosition = local;
            // The arrow sprite points down (-90°) unrotated.
            _gateArrow.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
        }

        private void TickCrush(float now)
        {
            int streak = _source.CrushStreak;
            if (streak >= CRUSH_SHOWN_FROM)
            {
                if (streak != _shownCrush)
                {
                    _shownCrush = streak;
                    _crushText.SetText(_crushFormat, streak);
                }
                _crushEndedAt = -1f;
                if (_crushGroup.alpha < 1f)
                    _crushGroup.alpha = 1f;
                return;
            }

            if (_crushGroup.alpha <= 0f)
                return;

            if (_crushEndedAt < 0f)
                _crushEndedAt = now;
            float alpha = 1f - (now - _crushEndedAt) / CRUSH_FADE;
            _crushGroup.alpha = alpha > 0f ? alpha : 0f;
            if (alpha <= 0f)
                _shownCrush = -1;
        }

        // ---------- Arena hints ----------

        /// <summary>Once per arena: the key hints go to HudScreen, crush and destroy cover to the panel here.</summary>
        private void StartArenaHints(ArenaHint hints, float now)
        {
            ArenaStarted?.Invoke(hints);

            bool crush = (hints & ArenaHint.Crush) != 0;
            bool cover = (hints & ArenaHint.DestroyCover) != 0;
            if (!crush && !cover)
            {
                _arenaHintShownAt = -1f;
                _arenaHintGroup.gameObject.SetActive(false);
                return;
            }

            // Built once per arena start, not per frame.
            _arenaHintText.text = crush && cover
                ? UiStrings.Get("run.hud_hint_crush") + "\n" + UiStrings.Get("run.hud_hint_destroy_cover")
                : UiStrings.Get(crush ? "run.hud_hint_crush" : "run.hud_hint_destroy_cover");
            _arenaHintShownAt = now;
            _arenaHintGroup.alpha = 1f;
            _arenaHintGroup.gameObject.SetActive(true);
        }

        private void TickArenaHint(float now)
        {
            if (_arenaHintShownAt < 0f)
                return;

            float fade = (now - _arenaHintShownAt - ARENA_HINT_SHOWN_FOR) / ARENA_HINT_FADE;
            if (fade <= 0f)
                return;

            if (fade >= 1f)
            {
                _arenaHintShownAt = -1f;
                _arenaHintGroup.gameObject.SetActive(false);
            }
            else
            {
                _arenaHintGroup.alpha = 1f - fade;
            }
        }

        private static void SetFill(Image image, float value)
        {
            if (Mathf.Abs(image.fillAmount - value) > FILL_EPSILON || (value >= 1f && image.fillAmount < 1f))
                image.fillAmount = value;
        }
    }
}
