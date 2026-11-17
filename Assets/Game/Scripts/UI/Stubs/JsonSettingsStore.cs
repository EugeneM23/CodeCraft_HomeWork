using System;

namespace Game
{
    /// <summary>
    /// STUB settings storage until the core team's SaveSystem: settings.json in persistentDataPath.
    /// Same contract as the core's store: Save writes, makes the copy Current, applies graphics and
    /// raises Changed. Without a file name it lives in memory only (tests).
    /// </summary>
    public sealed class JsonSettingsStore : ISettingsStore
    {
        public event Action<GameSettings> Changed;

        private readonly string _fileName;

        public GameSettings Current { get; private set; }

        public JsonSettingsStore(string fileName = "settings.json")
        {
            _fileName = fileName;
            Current = Load();
            SettingsApplyUseCase.ApplyGraphics(Current.graphics);
        }

        public GameSettings Load()
        {
            GameSettings settings = _fileName != null ? JsonFile.Read<GameSettings>(_fileName) : null;
            if (settings == null)
                return new GameSettings();

            settings.graphics ??= new GraphicsSettingsData();
            settings.audio ??= new AudioSettingsData();
            settings.controls ??= new ControlsSettingsData();
            return settings;
        }

        public void Save(GameSettings settings)
        {
            if (_fileName != null)
                JsonFile.Write(_fileName, settings);
            Current = settings;
            SettingsApplyUseCase.ApplyGraphics(settings.graphics);
            Changed?.Invoke(settings);
        }
    }
}
