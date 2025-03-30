using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Modules.Pools
{
    public abstract class DefaultPool<T> : MonoBehaviour where T : MonoBehaviour
    {
        
        [SerializeField] private T _prefab;

        public ObjectPool<T> Pool { get; private set; }
        protected List<T> _activeObjects = new List<T>(128);

        private void Awake()
        {
            Pool = new ObjectPool<T>(
                createFunc: CreateObject,
                actionOnGet: ActivateObject,
                actionOnRelease: DeactivateObject,
                actionOnDestroy: DestroyObject,
                collectionCheck: true,
                defaultCapacity: 64,
                maxSize: 1024);
        }

        private T CreateObject()
        {
            T obj = Instantiate(_prefab);
            OnCreate(obj);
            return obj;
        }

        private void ActivateObject(T obj)
        {
            obj.gameObject.SetActive(true);
            _activeObjects.Add(obj);
        }

        private void DeactivateObject(T obj) => obj.gameObject.SetActive(false);

        private void DestroyObject(T obj) => Destroy(obj.gameObject);

        public T Rent() => Pool.Get();

        public void Return(T obj)
        {
            _activeObjects.Remove(obj);
            DeactivateObject(obj);

            Pool.Release(obj);
        }

        protected abstract void OnCreate(T enemy);
    }
}