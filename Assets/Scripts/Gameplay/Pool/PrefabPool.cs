using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewPool
{
    public class PrefabPool : MonoBehaviour
    {
        private static PrefabPool _instance;

        public static PrefabPool Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject obj = new GameObject("PoolManager");
                    _instance = obj.AddComponent<PrefabPool>();
                    DontDestroyOnLoad(obj);
                }

                return _instance;
            }
        }

        private Dictionary<string, Queue<GameObject>> _pools = new();

        public T Spawn<T>(GameObject prefab) where T : MonoBehaviour, IDespawned
        {
            string key = prefab.name;

            if (!_pools.ContainsKey(key))
            {
                _pools[key] = new Queue<GameObject>();
            }

            if (_pools[key].Count > 0)
            {
                GameObject obj = _pools[key].Dequeue();
                obj.SetActive(true);
                return obj.GetComponent<T>();
            }

            return CreateObject(prefab).GetComponent<T>();
        }

        private GameObject CreateObject(GameObject prefab)
        {
            GameObject go = Instantiate(prefab);
            go.name = prefab.name; 
            
            IDespawned component = go.GetComponent<IDespawned>();
            component.SetDespawnCallBack(DeSpawn);
            
            return go;
        }

        public void DeSpawn(GameObject gameObject)
        {
            string key = gameObject.name;

            if (!_pools.ContainsKey(key))
            {
                _pools[key] = new Queue<GameObject>();
            }

            gameObject.SetActive(false);
            _pools[key].Enqueue(gameObject);
        }
    }
}