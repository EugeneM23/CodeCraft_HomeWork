using System.Collections.Generic;

namespace SaveLoadSystem
{
    public interface IGameSerializer
    {
        void Serialize(IDictionary<string, string> saveData);
        void Deserialize(IDictionary<string, string> LoadData);
    }
}