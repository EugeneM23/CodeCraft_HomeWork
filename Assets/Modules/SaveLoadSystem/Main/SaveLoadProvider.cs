using System.Collections.Generic;
using Zenject;

namespace SaveLoadSystem
{
    public class SaveLoadProvider
    {
        private readonly List<IGameSerializer> _serializeComponents;

        public SaveLoadProvider([InjectLocal] List<IGameSerializer> serializeComponents)
        {
            _serializeComponents = serializeComponents;
        }

        public void SerializeComponents(Dictionary<string, string> data)
        {
            foreach (IGameSerializer component in _serializeComponents)
                component.Serialize(data);
        }

        public void DeserializeComponents(Dictionary<string, string> data)
        {
            foreach (IGameSerializer component in _serializeComponents) 
                component.Deserialize(data);
        }
    }
}