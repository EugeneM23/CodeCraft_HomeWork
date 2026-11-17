using System;

namespace Game
{
    /// <summary>STUB progress storage: progress.json in persistentDataPath.</summary>
    public sealed class JsonPlayerProgress : IPlayerProgress
    {
        public event Action Changed;

        private readonly string _fileName;

        public PlayerProgressData Data { get; }

        public JsonPlayerProgress(string fileName = "progress.json")
        {
            _fileName = fileName;
            Data = JsonFile.Read<PlayerProgressData>(fileName) ?? new PlayerProgressData();
        }

        public void Save()
        {
            JsonFile.Write(_fileName, Data);
            Changed?.Invoke();
        }
    }
}
