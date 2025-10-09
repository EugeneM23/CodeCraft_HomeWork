using System.Collections.Generic;
using Modules.Entities;
using Newtonsoft.Json;
using SaveLoadSystem;
using UnityEngine;

namespace SampleGame.Gameplay
{
    public class EntityWorldSerializer : GameSerializer<EntityWorld, EntityWorldData>
    {
        protected override EntityWorldData Serialize(EntityWorld entityWorld)
        {
            Dictionary<int, string> entities = new();
            Dictionary<int, string> components = new();

            foreach (Entity entity in entityWorld.GetAll())
            {
                entities.Add(entity.Id, entity.Name);
                SerializeComponents(entity, components);
            }

            return new EntityWorldData(entities, components);
        }

        protected override void Deserialize(EntityWorld entityWorld, EntityWorldData data)
        {
            entityWorld.DestroyAll();

            foreach ((int id, string name) in data.Entities)
                LoadEntity(entityWorld, id, name);

            foreach (Entity entity in entityWorld.GetAll())
                DeSerializeComponents(data, entity.Id, entity);
        }

        private void LoadEntity(EntityWorld entityWorld, int id, string name)
        {
            if (!entityWorld.Has(id)) 
                entityWorld.Spawn(name, Vector3.zero, Quaternion.identity, id);
        }

        private static void DeSerializeComponents(EntityWorldData data, int id, Entity entity)
        {
            if (data.Components.ContainsKey(id))
            {
                string dataComponent = data.Components[id];
                var saveLoadProvider = entity.Context.Resolve<SaveLoadProvider>();
                var dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(dataComponent);
                saveLoadProvider.DeserializeComponents(dictionary);
            }
        }

        private static void SerializeComponents(Entity entity, Dictionary<int, string> components)
        {
            Dictionary<string, string> data = new();

            var saveLoadProvider = entity.Context.Resolve<SaveLoadProvider>();
            saveLoadProvider.SerializeComponents(data);

            components[entity.Id] = JsonConvert.SerializeObject(data);
        }

    }
}