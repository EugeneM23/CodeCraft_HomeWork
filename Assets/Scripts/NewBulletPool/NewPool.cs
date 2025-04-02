using System.Collections.Generic;
using Gameplay;
using UnityEngine;

namespace NewPool
{
    /*public class NewPool<T> where T : MonoBehaviour, IDespawned<T>
    {
        private T _prefab;
        private Transform _container;

        public NewPool(T prefab)
        {
            _prefab = prefab;
        }

        private readonly Queue<T> objects = new();

        public T Rent()
        {
            if (!this.objects.TryDequeue(out T obj))
                obj = Object.Instantiate(_prefab, _container);

            obj.gameObject.SetActive(true);

            obj.SetDespawnCallBack(Return);
            return obj;
        }

        public void Return(T obj)
        {
            obj.gameObject.SetActive(false);
            this.objects.Enqueue(obj);
        }
    }*/
}