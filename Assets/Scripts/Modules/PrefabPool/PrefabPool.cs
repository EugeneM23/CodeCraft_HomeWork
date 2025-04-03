using System.Collections.Generic;
using UnityEngine;

namespace Modules.PrefabPool
{
    public class PrefabPool
    {
        private Dictionary<string, Queue<GameObject>> _pools = new();

        public T Spawn<T>(GameObject prefab) where T : MonoBehaviour, IDespawned
        {
            string key = prefab.name;

            if (!_pools.ContainsKey(key)) 
                _pools[key] = new Queue<GameObject>();

            if (_pools[key].Count > 0)
            {
                GameObject obj = _pools[key].Dequeue();
                obj.SetActive(true);
                obj.GetComponent<T>().DeSpawn += DeSpawn;

                return obj.GetComponent<T>();
            }

            return CreateObject(prefab).GetComponent<T>();
        }

        private GameObject CreateObject(GameObject prefab)
        {
            GameObject go = Object.Instantiate(prefab);
            go.name = prefab.name;

            IDespawned component = go.GetComponent<IDespawned>();
            component.DeSpawn += DeSpawn;
            return go;
        }

        public void DeSpawn(GameObject gameObject)
        {
            gameObject.GetComponent<IDespawned>().DeSpawn -= DeSpawn;
            string key = gameObject.name;


            gameObject.SetActive(false);
            _pools[key].Enqueue(gameObject);
        }
    }
}