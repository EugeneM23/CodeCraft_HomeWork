using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// STUB progress until the core team's SaveSystem: kept in progress.json in persistentDataPath and
    /// written after every change. Without a file name it lives in memory only (tests).
    /// </summary>
    public sealed class JsonPlayerProgress : IPlayerProgress
    {
        public event Action Changed;

        private readonly string _fileName;
        private readonly PlayerProgressData _data;

        public JsonPlayerProgress(string fileName = "progress.json")
        {
            _fileName = fileName;
            _data = (fileName != null ? JsonFile.Read<PlayerProgressData>(fileName) : null) ?? new PlayerProgressData();
        }

        public int Credits => _data.credits;
        public IReadOnlyCollection<string> UnlockedLevels => _data.unlockedLevels;

        public void AddCredits(int amount)
        {
            _data.credits += amount;
            Save();
        }

        public bool TrySpendCredits(int amount)
        {
            if (amount < 0 || _data.credits < amount)
                return false;

            _data.credits -= amount;
            Save();
            return true;
        }

        public bool OwnsModule(string moduleId) => GetModuleLevel(moduleId) > 0;

        public int GetModuleLevel(string moduleId)
        {
            foreach (PlayerProgressData.OwnedModule module in _data.modules)
                if (module.id == moduleId)
                    return module.level;
            return 0;
        }

        public void SetModuleLevel(string moduleId, int level)
        {
            PlayerProgressData.OwnedModule found = null;
            foreach (PlayerProgressData.OwnedModule module in _data.modules)
                if (module.id == moduleId)
                    found = module;

            if (found != null)
                found.level = level;
            else
                _data.modules.Add(new PlayerProgressData.OwnedModule { id = moduleId, level = level });
            Save();
        }

        public string GetInstalledModule(string slot)
        {
            foreach (PlayerProgressData.InstalledModule entry in _data.installed)
                if (entry.slot == slot)
                    return entry.id;
            return null;
        }

        public void InstallModule(string slot, string moduleId)
        {
            PlayerProgressData.InstalledModule found = null;
            foreach (PlayerProgressData.InstalledModule entry in _data.installed)
                if (entry.slot == slot)
                    found = entry;

            if (found != null)
                found.id = moduleId;
            else
                _data.installed.Add(new PlayerProgressData.InstalledModule { slot = slot, id = moduleId });
            Save();
        }

        public bool IsLevelUnlocked(string levelId) => _data.unlockedLevels.Contains(levelId);

        public void UnlockLevel(string levelId)
        {
            if (_data.unlockedLevels.Contains(levelId))
                return;
            _data.unlockedLevels.Add(levelId);
            Save();
        }

        public bool IsLevelCompleted(string levelId) => _data.completedLevels.Contains(levelId);

        public void MarkLevelCompleted(string levelId)
        {
            if (_data.completedLevels.Contains(levelId))
                return;
            _data.completedLevels.Add(levelId);
            Save();
        }

        private void Save()
        {
            if (_fileName != null)
                JsonFile.Write(_fileName, _data);
            Changed?.Invoke();
        }
    }
}
