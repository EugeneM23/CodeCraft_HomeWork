using System.Collections.Generic;
using UnityEngine;

namespace Inventories
{
    public class PrefabPool
    {
        private readonly Dictionary<string, Queue<GameObject>> _pools = new();

        public T Spawn<T>(GameObject prefab, RectTransform parent) where T : MonoBehaviour
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

            return CreateObject(prefab, parent).GetComponent<T>();
        }

        private GameObject CreateObject(GameObject prefab, RectTransform parent)
        {
            GameObject go = Object.Instantiate(prefab, parent);
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