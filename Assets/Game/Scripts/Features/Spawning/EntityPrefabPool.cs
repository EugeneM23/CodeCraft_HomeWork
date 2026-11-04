using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Project-local replacement for the removed Atomic.Entities.SceneEntityPool.
    /// Behaviour is identical to the old framework type: one pool per prefab, entities
    /// parked inactive under a container transform.
    /// </summary>
    /// <remarks>
    /// The framework's own <see cref="PrefabEntityPool"/> keys its pools by GameObject name
    /// and is a MonoBehaviour, while <see cref="GameFactory"/> keys by the EntityID value its
    /// installers write. Keeping this type preserves that behaviour; swapping to
    /// PrefabEntityPool is a separate, behaviour-changing step.
    /// </remarks>
    public class EntityPrefabPool
    {
        private readonly MonoEntity _prefab;
        private readonly Transform _container;
        private readonly Queue<MonoEntity> _queue = new();

        public EntityPrefabPool(MonoEntity prefab, Transform container)
        {
            _prefab = prefab;
            _container = container;
        }

        /// <summary>How many instances are parked and ready to be rented.</summary>
        public int Count => _queue.Count;

        public void Init(int count)
        {
            for (int i = 0; i < count; i++)
            {
                MonoEntity entity = MonoEntity.Create(_prefab, _container);
                this.OnCreate(entity);
                _queue.Enqueue(entity);
            }
        }

        public IEntity Rent()
        {
            if (!_queue.TryDequeue(out MonoEntity entity))
            {
                entity = MonoEntity.Create(_prefab, _container);
                this.OnCreate(entity);
            }

            this.OnRent(entity);
            return entity;
        }

        public void Return(IEntity entity)
        {
            if (MonoEntity.TryCast(entity, out MonoEntity monoEntity) && !_queue.Contains(monoEntity))
            {
                this.OnReturn(monoEntity);
                _queue.Enqueue(monoEntity);
            }
        }

        public void Clear()
        {
            foreach (MonoEntity entity in _queue)
                Object.Destroy(entity);

            _queue.Clear();
        }

        protected virtual void OnCreate(MonoEntity entity) => entity.gameObject.SetActive(false);

        protected virtual void OnRent(MonoEntity entity) => entity.gameObject.SetActive(true);

        protected virtual void OnReturn(MonoEntity entity)
        {
            entity.gameObject.SetActive(false);
            entity.transform.SetParent(_container);
        }
    }
}
