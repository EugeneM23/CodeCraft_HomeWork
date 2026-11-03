using System;
using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Pool of pooled entity prefabs, keyed both by prefab reference and by the EntityID its
    /// installers write.
    /// </summary>
    /// <remarks>
    /// The prefab dictionary exists to keep the spawn path allocation-free. Create used to derive
    /// a string key with <c>prefab.name.Replace("(Clone)", "")</c>: GameObject.name marshals a new
    /// string out of native Unity on every call and Replace allocated a second one, even though a
    /// prefab name never contains "(Clone)". That ran for every bullet, shell and impact effect.
    /// </remarks>
    public class GameFactory
    {
        private readonly Dictionary<MonoEntity, EntityPrefabPool> _poolsByPrefab = new();
        private readonly Dictionary<string, EntityPrefabPool> _poolsById = new();
        private readonly Transform _poolsParent;

        public GameFactory(Transform poolsParent)
        {
            _poolsParent = poolsParent;
        }

        /// <summary>Registered pools by id. For debug readouts — do not mutate.</summary>
        public IReadOnlyDictionary<string, EntityPrefabPool> Pools => _poolsById;

        public IEntity Create(MonoEntity prefab)
        {
            if (!_poolsByPrefab.TryGetValue(prefab, out EntityPrefabPool pool))
                pool = RegisterNewPrefab(prefab, prefab.name);

            return pool.Rent();
        }

        public IEntity Create(string id)
        {
            if (!_poolsById.TryGetValue(id, out EntityPrefabPool pool))
                throw new InvalidOperationException(
                    $"GameFactory: no pool registered for id '{id}'. " +
                    "Register the prefab in GameContextInstaller before spawning it.");

            return pool.Rent();
        }

        /// <param name="prewarm">
        /// Instances to instantiate up front. Left at zero, the pool grows by a synchronous
        /// Instantiate during the first burst of fire, which is the frame spike it exists to avoid.
        /// </param>
        public EntityPrefabPool RegisterNewPrefab(MonoEntity prefab, string id, int prewarm = 0)
        {
            GameObject poolContainer = new GameObject($"Pool_{id}");
            poolContainer.transform.SetParent(_poolsParent);

            EntityPrefabPool pool = new EntityPrefabPool(prefab, poolContainer.transform);

            if (prewarm > 0)
                pool.Init(prewarm);

            _poolsByPrefab[prefab] = pool;
            _poolsById[id] = pool;
            return pool;
        }

        public void Destroy(IEntity entity)
        {
            string entityID = entity.GetEntityID();

            // No pool for this id means the entity was never rented from one: the buff and dash
            // pickups are placed in the scene by hand and still go through here. Destroy it for
            // real instead of pooling it.
            //
            // This used to recover by registering the live instance as the pool prefab, which is
            // what must not happen: for characters GetTransform() is the character root rather
            // than the entity root, so the component was null and the next Rent instantiated
            // null; when it was found, the pool started cloning a mutated, deactivated runtime
            // object whose values were already populated.
            if (!_poolsById.TryGetValue(entityID, out EntityPrefabPool pool))
            {
                if (MonoEntity.TryCast(entity, out MonoEntity monoEntity))
                    MonoEntity.Destroy(monoEntity);

                return;
            }

            pool.Return(entity);
        }
    }
}
