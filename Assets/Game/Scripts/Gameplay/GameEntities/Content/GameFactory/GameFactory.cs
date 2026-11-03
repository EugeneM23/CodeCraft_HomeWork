using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class GameFactory
    {
        private readonly ReactiveDictionary<string, EntityPrefabPool> _pools = new();
        private readonly Transform _poolsParent;

        public GameFactory(Transform poolsParent)
        {
            _poolsParent = poolsParent;
        }

        public IEntity Create(MonoEntity prefab)
        {
            string id = prefab.gameObject.name.Replace("(Clone)", "");

            if (!_pools.ContainsKey(id))
                RegisterNewPrefab(prefab, id);

            return Create(id);
        }

        public IEntity Create(string id)
        {
            IEntity entity = _pools[id].Rent();
            return entity;
        }

        public void RegisterNewPrefab(MonoEntity prefab, string id)
        {
            GameObject poolContainer = new GameObject($"Pool_{id}");
            poolContainer.transform.SetParent(_poolsParent);

            EntityPrefabPool pool = new EntityPrefabPool(prefab, poolContainer.transform);
            _pools[id] = pool;
        }

        public void Destroy(IEntity entity)
        {
            string entityID = entity.GetEntityID();

            if (!_pools.ContainsKey(entityID))
            {
                RegisterNewPrefab(entity.GetTransform().GetComponent<MonoEntity>(), entityID);
            }

            _pools[entityID].Return(entity);
        }
    }
}