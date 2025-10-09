using System.Collections.Generic;
using Newtonsoft.Json;

namespace SaveLoadSystem
{
    public class SaveLoadService
    {
        private readonly IGameRepository _gameRepository;
        private readonly List<IGameSerializer> _gameSerializers;

        public SaveLoadService(IGameRepository gameRepository, List<IGameSerializer> gameSerializers)
        {
            _gameRepository = gameRepository;
            _gameSerializers = gameSerializers;
        }

        public bool Save()
        {
            var dictionary = new Dictionary<string, string>();

            foreach (IGameSerializer gameSerializer in _gameSerializers)
                gameSerializer.Serialize(dictionary);

            _gameRepository.SaveData(dictionary);

            return true;
        }

        public bool Load()
        {
            Dictionary<string, string> loadData = _gameRepository.LoadData();

            foreach (IGameSerializer gameSerializer in _gameSerializers)
                gameSerializer.Deserialize(loadData);

            return true;
        }

        public void AddSerializer(IGameSerializer serializer)
        {
            _gameSerializers.Add(serializer);
        }
    }
}