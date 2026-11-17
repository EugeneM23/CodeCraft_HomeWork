using System;
using Saving;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The settings on disk: their own save (slot 0, section "settings"), apart from the progress. Load at the start of
    /// the game makes <see cref="Current"/>; screens edit a copy and give it to <see cref="Save"/>, which writes it,
    /// makes it Current, applies the graphics and raises <see cref="Changed"/>. Audio and controls listen to Changed.
    /// </summary>
    public sealed class SettingsStore : ISettingsStore
    {
        public const int SLOT = 0;

        private readonly SaveLoadService _saves;
        private GameSettings _read;

        public SettingsStore(ISaveStorage storage, string gameVersion)
        {
            Current = new GameSettings();
            _saves = new SaveLoadService(storage, gameVersion);
            _saves.Register(new SettingsSerializer(this));
        }

        public GameSettings Current { get; private set; }

        /// <summary>What the last Load found: Empty on a first run, NewerVersion for a file of a newer game...</summary>
        public LoadOutcome LastLoadOutcome { get; private set; } = LoadOutcome.Empty;

        /// <summary>True when the file was written by a newer game or failed to migrate: saving writes nothing.</summary>
        public bool IsReadOnly => _saves.IsReadOnly;

        public event Action<GameSettings> Changed;

        /// <summary>
        /// Reads the file into <see cref="Current"/> (defaults when there is none or it cannot be read), raises Changed
        /// and returns a copy. Does not apply: call <see cref="Apply"/>.
        /// </summary>
        public GameSettings Load()
        {
            _read = null;
            LastLoadOutcome = _saves.Load();
            GameSettings loaded = _read ?? new GameSettings();
            _read = null;

            Current = loaded;
            Changed?.Invoke(Current);
            return loaded.Clone();
        }

        public void Save(GameSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Current = Normalize(settings.Clone());
            SaveCurrent();
            Apply();
            Changed?.Invoke(Current);
        }

        /// <summary>Writes <see cref="Current"/> when it differs from the file (app pause, quit).</summary>
        public SaveOutcome SaveCurrent() => _saves.Save(onlyIfChanged: true);

        /// <summary>Quality, vSync, frame rate limit; the resolution and the window mode only in a build.</summary>
        public void Apply()
        {
            GraphicsSettingsData graphics = Current.graphics;

            if (graphics.qualityLevel >= 0 && graphics.qualityLevel < QualitySettings.names.Length
                && graphics.qualityLevel != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(graphics.qualityLevel, applyExpensiveChanges: true);

            // After the quality level: a level carries its own vSync
            QualitySettings.vSyncCount = graphics.vSync ? 1 : 0;
            Application.targetFrameRate = graphics.frameRateLimit > 0 ? graphics.frameRateLimit : -1;

            if (Application.isEditor || graphics.width <= 0 || graphics.height <= 0)
                return;

            if (Screen.width == graphics.width && Screen.height == graphics.height
                && Screen.fullScreenMode == graphics.windowMode)
                return;

            RefreshRate rate = graphics.refreshRateHz > 0f
                ? new RefreshRate { numerator = (uint)Mathf.RoundToInt(graphics.refreshRateHz * 1000f), denominator = 1000 }
                : Screen.currentResolution.refreshRateRatio;
            Screen.SetResolution(graphics.width, graphics.height, graphics.windowMode, rate);
        }

        /// <summary>The serializer hands over the section read by Load.</summary>
        internal void Restore(GameSettings data) => _read = Normalize(data ?? new GameSettings());

        private static GameSettings Normalize(GameSettings settings)
        {
            settings.graphics ??= new GraphicsSettingsData();
            settings.audio ??= new AudioSettingsData();
            settings.controls ??= new ControlsSettingsData();
            settings.controls.bindingOverrides ??= string.Empty;
            return settings;
        }
    }
}
