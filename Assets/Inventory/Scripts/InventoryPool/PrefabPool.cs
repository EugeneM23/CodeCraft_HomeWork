using System.Collections.Generic;
using UnityEngine;

namespace Inventories
{
    public class PrefabPool
    {
        private readonly Dictionary<string, Queue<GameObject>> _pools = new();

        public T Spawn<T>(GameObject prefab, RectTransform parent = null) where T : MonoBehaviour
        {
            string key = prefab.name;

            if (!_pools.ContainsKey(key))
                _pools[key] = new Queue<GameObject>();

            if (_pools[key].Count > 0)
            {
                GameObject obj = _pools[key].Dequeue();
                obj.SetActive(true);

                return obj.GetComponent<T>();
            }

            if (parent != null)
            {
                return CreateObject(prefab, parent).GetComponent<T>();
            }

            return CreateObject(prefab).GetComponent<T>();
        }

        private GameObject CreateObject(GameObject prefab, RectTransform parent = null)
        {
            GameObject go;
            if (parent != null)
                go = Object.Instantiate(prefab, parent);
            else
                go = Object.Instantiate(prefab);

            go.gameObject.name = prefab.name;

            return go;
        }

        public void DeSpawn(GameObject gameObject)
        {
            string key = gameObject.name;

            gameObject.SetActive(false);
            if (_pools.TryGetValue(key, out var pool))
                pool.Enqueue(gameObject);
            else
                _pools.Add(key, new Queue<GameObject>());
        }
    }
}