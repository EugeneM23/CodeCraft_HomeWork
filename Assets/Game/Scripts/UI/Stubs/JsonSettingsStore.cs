namespace Game
{
    /// <summary>STUB settings storage: settings.json in persistentDataPath.</summary>
    public sealed class JsonSettingsStore : ISettingsStore
    {
        private readonly string _fileName;

        public JsonSettingsStore(string fileName = "settings.json") => _fileName = fileName;

        public GameSettings Load()
        {
            GameSettings settings = JsonFile.Read<GameSettings>(_fileName);
            if (settings == null || settings.version != GameSettings.CURRENT_VERSION)
                return GameSettings.CreateDefault();

            settings.graphics ??= new GameSettings.Graphics();
            settings.audio ??= new GameSettings.Audio();
            settings.controls ??= new GameSettings.Controls();
            return settings;
        }

        public void Save(GameSettings settings) => JsonFile.Write(_fileName, settings);
    }
}
