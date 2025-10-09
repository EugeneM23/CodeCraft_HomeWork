using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace SaveLoadSystem
{
    public interface IGameDedicatedRepository
    {
        UniTask Save(Dictionary<string, string> saveData);

        UniTask<Dictionary<string, string>> Load();
    }
}