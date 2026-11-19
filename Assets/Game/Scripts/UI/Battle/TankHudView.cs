using System;
using Atomic.Elements;
using Atomic.Entities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The tank's part of the HUD: the hull's health bar (the tank's real max), the cannon's reload (bar, seconds and the
    /// arc round the crosshair), the machine gun's ammo and the boost lamp. Follows PlayerContext.Character
    /// (the tank while the player drives it); parts the character lacks are hidden. Health, ammo and boost
    /// come by their events; the cooldowns have none and are read in Tick, from cached fields, without
    /// allocating. HudScreen calls Tick every frame.
    /// </summary>
    public sealed class TankHudView : MonoBehaviour
    {
        private const float FILL_EPSILON = 0.002f;

        [Header("Hull")]
        [SerializeField] private Image _healthFill;
        [SerializeField] private TMP_Text _healthText;

        [Header("Cannon")]
        [SerializeField] private GameObject _gunGroup;
        [SerializeField] private Image _gunFill;
        [SerializeField] private TMP_Text _gunText;
        [Tooltip("Filled Radial360 image round the crosshair, shown while the cannon reloads")]
        [SerializeField] private Image _reloadArc;
        [Tooltip("The crosshair's lines, tinted while the cannon reloads")]
        [SerializeField] private Graphic[] _crosshair;

        [Header("Machine gun")]
        [SerializeField] private GameObject _mgGroup;
        [SerializeField] private Image _mgFill;
        [SerializeField] private TMP_Text _mgText;

        [Header("Boost")]
        [SerializeField] private GameObject _boostGroup;
        [SerializeField] private Image _boostLamp;

        private Subscription<IEntity> _characterSubscription;
        private Subscription<IEntity> _weaponSubscription;
        private Subscription<IEntity> _secondarySubscription;
        private Subscription<bool> _boostSubscription;

        private Health _health;
        private CooldownTimer _gunCooldown;
        private CooldownTimer _mgCooldown;
        private Ammo _mgAmmo;
        private int _mgAmmoMax;

        private int _gunTenths = -1;
        private int _mgTenths = -1;
        private bool _gunReady = true;
        private bool _gunReadyKnown;

        private Action<IEntity> _onCharacterChanged;
        private Action<IEntity> _onWeaponChanged;
        private Action<IEntity> _onSecondaryChanged;
        private Action<bool> _onBoostChanged;
        private Action<int> _onHealthChanged;
        private Action _onAmmoChanged;

        // From UiStrings, fetched once in Awake
        private string _readyText;
        private string _reloadFormat;

        private void Awake()
        {
            _readyText = UiStrings.Get("hud.ready");
            _reloadFormat = UiStrings.Get("hud.reload_seconds_format");

            _onCharacterChanged = HandleCharacterChanged;
            _onWeaponChanged = HandleWeaponChanged;
            _onSecondaryChanged = HandleSecondaryChanged;
            _onBoostChanged = HandleBoostChanged;
            _onHealthChanged = HandleHealthChanged;
            _onAmmoChanged = RefreshAmmo;
        }

        private void OnEnable()
        {
            if (GameContext.TryGetInstance(out GameContext context))
                _characterSubscription = context.GetPlayerContext().GetCharacter().Observe(_onCharacterChanged);
            else
                HandleCharacterChanged(null);
        }

        private void OnDisable()
        {
            _characterSubscription.Dispose();
            _characterSubscription = default;
            Release();
        }

        /// <summary>Per frame: the cooldowns, which raise no events. No allocations.</summary>
        public void Tick()
        {
            if (_gunCooldown != null)
                TickGun();

            if (_mgCooldown != null && _mgAmmo == null)
                TickCooldownBar(_mgCooldown, _mgFill, _mgText, ref _mgTenths);
        }

        // The cannon's shell type in its label (ПУШКА · ОФ / ББ), switched by Q (AmmoUseCase).
        private IEntity _gunWeapon;
        private UiLocalizedText _gunLabel;
        private int _shownAmmo = -1;

        private void TickGun()
        {
            bool ready = _gunCooldown.IsExpired();
            TickCooldownBar(_gunCooldown, _gunFill, _gunText, ref _gunTenths);

            int ammo = (int)AmmoUseCase.Loaded(_gunWeapon);
            if (ammo != _shownAmmo && _gunLabel != null)
            {
                _shownAmmo = ammo;
                _gunLabel.Key = ammo == (int)ShellKind.ArmourPiercing ? "hud.gun_ap" : "hud.gun_he";
            }

            if (!ready)
                SetFill(_reloadArc, 1f - Remaining(_gunCooldown));

            if (_gunReadyKnown && ready == _gunReady)
                return;

            _gunReadyKnown = true;
            _gunReady = ready;
            _reloadArc.enabled = !ready;
            Color tint = ready ? UiStyle.Text : UiStyle.Disabled;
            for (int i = 0; i < _crosshair.Length; i++)
                _crosshair[i].color = tint;
        }

        // Bar fills as the weapon reloads; the text shows seconds left to a tenth, or "ready".
        private void TickCooldownBar(CooldownTimer cooldown, Image fill, TMP_Text text, ref int shownTenths)
        {
            float remaining = Remaining(cooldown);
            SetFill(fill, 1f - remaining);

            float seconds = remaining * cooldown.Duration;
            int tenths = Mathf.CeilToInt(seconds * 10f);
            if (tenths == shownTenths)
                return;

            shownTenths = tenths;
            if (tenths <= 0)
                text.SetText(_readyText);
            else if (tenths < MAX_CACHED_TENTHS)
                text.SetText(ReloadText(tenths));
            else
                text.SetText(_reloadFormat, tenths / 10f);
        }

        /// <summary>Reload times (tenths of a second) whose text is kept once made.</summary>
        private const int MAX_CACHED_TENTHS = 600;

        private readonly string[] _reloadTexts = new string[MAX_CACHED_TENTHS];

        /// <summary>
        /// The reload text for <paramref name="tenths"/>, made once: SetText(format, float) builds a string on every
        /// call in the editor (TMP keeps m_text there), every 0.1 s while a gun reloads; SetText(string) does not.
        /// </summary>
        private string ReloadText(int tenths)
        {
            string text = _reloadTexts[tenths];
            if (text == null)
                _reloadTexts[tenths] = text = FormatTmp(_reloadFormat, tenths / 10f);
            return text;
        }

        /// <summary>A TMP SetText format with one float ("{0:1}" — one decimal) as a plain string.</summary>
        private static string FormatTmp(string format, float value)
        {
            int open = format.IndexOf("{0", StringComparison.Ordinal);
            int close = open >= 0 ? format.IndexOf('}', open) : -1;
            if (close < 0)
                return format;

            // As TMP reads it: a digit 1-9 is the decimals ("{0:1}"), or the zeros after a dot ("{0:0.00}").
            int decimals = 0;
            bool afterDot = false;
            for (int i = open + 2; i < close; i++)
                if (format[i] == '.')
                    afterDot = true;
                else if (format[i] >= '1' && format[i] <= '9')
                    decimals = format[i] - '0';
                else if (format[i] == '0' && afterDot)
                    decimals++;
            string number = value.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture);
            return format.Substring(0, open) + number + format.Substring(close + 1);
        }

        private static float Remaining(CooldownTimer cooldown) =>
            cooldown.Duration > 0f ? Mathf.Clamp01(cooldown.GetProgress()) : 0f;

        private static void SetFill(Image image, float value)
        {
            if (Mathf.Abs(image.fillAmount - value) > FILL_EPSILON || (value >= 1f && image.fillAmount < 1f))
                image.fillAmount = value;
        }

        // ---------- Character ----------

        private void HandleCharacterChanged(IEntity character)
        {
            Release();

            if (character != null && character.TryGetHealth(out _health))
            {
                _health.OnHealthChanged += _onHealthChanged;
                _health.OnMaxHealthChanged += _onHealthChanged;
            }
            RefreshHealth();

            if (character != null && character.TryGetWeapon(out IReactiveVariable<IEntity> weapon))
                _weaponSubscription = weapon.Observe(_onWeaponChanged);
            else
                HandleWeaponChanged(null);

            if (character != null && character.TryGetSecondaryWeapon(out IReactiveVariable<IEntity> secondary))
                _secondarySubscription = secondary.Observe(_onSecondaryChanged);
            else
                HandleSecondaryChanged(null);

            if (character != null && character.TryGetDriveBoost(out IReactiveVariable<bool> boost))
            {
                _boostGroup.SetActive(true);
                _boostSubscription = boost.Observe(_onBoostChanged);
            }
            else
            {
                _boostGroup.SetActive(false);
            }
        }

        private void Release()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= _onHealthChanged;
                _health.OnMaxHealthChanged -= _onHealthChanged;
                _health = null;
            }

            _weaponSubscription.Dispose();
            _weaponSubscription = default;
            _secondarySubscription.Dispose();
            _secondarySubscription = default;
            _boostSubscription.Dispose();
            _boostSubscription = default;
            SetAmmo(null);
            _gunCooldown = null;
            _gunWeapon = null;
            _mgCooldown = null;
        }

        private void HandleHealthChanged(int _) => RefreshHealth();

        private void RefreshHealth()
        {
            if (_health == null)
            {
                _healthFill.fillAmount = 0f;
                _healthText.SetText("—");
                return;
            }

            _healthFill.fillAmount = _health.GetMax() > 0 ? _health.GetPercent() : 0f;
            _healthText.SetText("{0:0}/{1:0}", _health.GetCurrent(), _health.GetMax());
        }

        private void HandleWeaponChanged(IEntity weapon)
        {
            _gunCooldown = null;
            _gunWeapon = weapon;
            _shownAmmo = -1;
            if (_gunLabel == null && _gunGroup != null)
                _gunLabel = _gunGroup.GetComponentInChildren<UiLocalizedText>(true);
            if (weapon != null)
                weapon.TryGetWeaponCooldown(out _gunCooldown);

            bool has = _gunCooldown != null;
            _gunGroup.SetActive(has);
            _gunTenths = -1;
            _gunReadyKnown = false;
            if (!has)
            {
                _reloadArc.enabled = false;
                for (int i = 0; i < _crosshair.Length; i++)
                    _crosshair[i].color = UiStyle.Text;
            }
        }

        private void HandleSecondaryChanged(IEntity weapon)
        {
            SetAmmo(null);
            _mgCooldown = null;
            _mgTenths = -1;

            if (weapon != null && weapon.TryGetAmmo(out Ammo ammo))
                SetAmmo(ammo);
            else if (weapon != null)
                weapon.TryGetWeaponCooldown(out _mgCooldown);

            _mgGroup.SetActive(_mgAmmo != null || _mgCooldown != null);
        }

        // Ammo has no maximum of its own: the count at hand-over is taken as full and grows with pickups.
        private void SetAmmo(Ammo ammo)
        {
            if (_mgAmmo != null)
                _mgAmmo.OnStateChanged -= _onAmmoChanged;

            _mgAmmo = ammo;
            if (_mgAmmo == null)
                return;

            _mgAmmoMax = Mathf.Max(1, _mgAmmo.GetCount());
            _mgAmmo.OnStateChanged += _onAmmoChanged;
            RefreshAmmo();
        }

        private void RefreshAmmo()
        {
            int count = _mgAmmo.GetCount();
            if (count > _mgAmmoMax)
                _mgAmmoMax = count;

            _mgFill.fillAmount = (float)count / _mgAmmoMax;
            _mgText.SetText("{0:0}/{1:0}", count, _mgAmmoMax);
        }

        private void HandleBoostChanged(bool on) => _boostLamp.color = on ? UiStyle.Accent : UiStyle.Disabled;
    }
}
