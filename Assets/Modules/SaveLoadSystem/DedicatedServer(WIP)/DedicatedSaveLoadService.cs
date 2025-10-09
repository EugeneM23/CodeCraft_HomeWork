using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SaveLoadSystem
{
    public class DedicatedSaveLoadService
    {
        private readonly IGameDedicatedRepository _gameDedicatedRepository;
        private readonly IEnumerable<IGameSerializer> _gameSerializers;

        public DedicatedSaveLoadService(IEnumerable<IGameSerializer> gameSerializers,
            IGameDedicatedRepository gameDedicatedRepository)
        {
            _gameSerializers = gameSerializers;
            _gameDedicatedRepository = gameDedicatedRepository;
        }

        public async UniTaskVoid Save()
        {
            var dictionary = new Dictionary<string, string>();

            foreach (IGameSerializer gameSerializer in _gameSerializers)
                gameSerializer.Serialize(dictionary);

            await _gameDedicatedRepository.Save(dictionary);
            Debug.Log("Saved!");
        }

        public async UniTask Load()
        {
            Dictionary<string, string> dictionary = await _gameDedicatedRepository.Load();

            foreach (IGameSerializer gameSerializer in _gameSerializers)
                gameSerializer.Deserialize(dictionary);

            Debug.Log("Load!");
        }
     }
}