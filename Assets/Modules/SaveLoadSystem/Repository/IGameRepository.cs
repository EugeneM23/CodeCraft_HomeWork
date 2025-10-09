using System.Collections.Generic;

namespace SaveLoadSystem
{
    public interface IGameRepository
    {
        Dictionary<string, string> LoadData();
        void SaveData(Dictionary<string, string> data);
    }
}