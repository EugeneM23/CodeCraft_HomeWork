using System.Collections.Generic;
using UnityEngine;

namespace MidniteOilSoftware.ObjectPoolManager
{
    public class ObjectPoolManager : MonoBehaviour
    {
        #region Private members

        private static ObjectPoolManager _instance;
        [SerializeField] private int _initialCapacity = 32;
        private readonly Dictionary<string, Queue<GameObject>> _pool = new();

        #endregion Private Members

        #region Unity Callbacks

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion Unity Callbacks

        public void SetInitialCapacity(int initialCapacity)
        {
            _initialCapacity = initialCapacity;
        }
        
        #region Spawning

        public static GameObject SpawnGameObject(GameObject prefab, bool setActive = true)
        {
            if (_instance == null) return null;

            GameObject go = _instance.DequeGameObject(prefab);
            if (go != null)
            {
                go.SetActive(setActive);
            }
            else
            {
                go = _instance.InstantiateGameObject(prefab, setActive);
            }

            return go;
        }

      
        public static GameObject SpawnGameObject(GameObject prefab, Vector3 position, Quaternion rotation,
            bool setActive = true)
        {
            if (_instance == null) return null;
            GameObject go = _instance.DequeGameObject(prefab);
            if (go != null)
            {
                go.transform.position = position;
                go.transform.rotation = rotation;
                go.SetActive(setActive);
            }
            else
            {
                go = _instance.InstantiateGameObject(prefab, position, rotation, setActive);
            }

            return go;
        }

        #endregion Spawning

        #region Despawning / Destroying

        public static void DespawnGameObject(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            go.GetComponent<IDespawnedPoolObject>()?.ReturnedToPool();
            var pool = _instance.GetPool(go);
            pool.Enqueue(go);
        }

        public static void PermanentlyDestroyGameObjectsOfType(GameObject prefab)
        {
            if (_instance == null) return;
            var queue = _instance.GetPool(prefab);
            GameObject go;
            while (queue?.Count > 0)
            {
                go = queue.Dequeue();
                if (go != null)
                {
                    if (go.activeSelf)
                    {
                        go.SetActive(false);
                    }

                    Destroy(go);
                }
            }
        }

        public static void EmptyPool(GameObject prefab=null)
        {
            if (_instance == null) return;
            if (prefab != null)
            {
                var pool = _instance.GetPool(prefab);
                if (pool == null) return;
                while (pool.Count > 0)
                {
                    var go = pool.Dequeue();
                    if (go != null)
                    {
                        Destroy(go);
                    }
                }
                pool.Clear();
                return;
            }
            foreach (Queue<GameObject> pool in _instance._pool.Values)
            {
                while (pool.Count > 0)
                {
                    GameObject go = pool.Dequeue();
                    if (go != null)
                    {
                        Destroy(go);
                    }
                }
            }

            _instance._pool.Clear();
        }

        #endregion

        #region Private methods

        private GameObject DequeGameObject(GameObject prefab)
        {
            var queue = GetPool(prefab);
            if (queue.Count < 1) return null;
            GameObject go = queue.Dequeue();
            if (go == null)
            {
                Debug.LogWarning("Dequeued null gameObject (" + prefab.name + ") from pool.");
            }
            go.GetComponent<IRetrievedPoolObject>()?.RetrievedFromPool(prefab);
            return go;
        }

        private GameObject InstantiateGameObject(GameObject prefab, bool setActive)
        {
            var queue = GetPool(prefab);
            var go = Instantiate(prefab);
            DontDestroyOnLoad(go);
            go.SetActive(setActive);
            go.name = prefab.name;
            return go;
        }

        private GameObject InstantiateGameObject(GameObject prefab, Vector3 position, Quaternion rotation,
            bool setActive)
        {
            GameObject go = Instantiate(prefab, position, rotation);
            DontDestroyOnLoad(go);
            go.SetActive(setActive);
            go.name = prefab.name;
            return go;
        }

        private Queue<GameObject> GetPool(GameObject prefab)
        {
            Queue<GameObject> pool;

            if (_pool.ContainsKey(prefab.name))
            {
                pool = _pool[prefab.name];
            }
            else
            {
                pool = new Queue<GameObject>(_initialCapacity);
                _pool.Add(prefab.name, pool);
            }

            return pool;
        }

        #endregion
    }
}
