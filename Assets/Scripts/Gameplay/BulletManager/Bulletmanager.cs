using System;
using System.Collections.Generic;
using Modules.PrefabPool;
using UnityEngine;

namespace Gameplay
{
    public class Bulletmanager : MonoBehaviour
    {
        private PrefabPool _prefabPoll;
        private List<IDespawned> _activeObjects = new List<IDespawned>();

        private void Awake()
        {
            _prefabPoll = new PrefabPool();
        }

        public T GetBullet<T>(GameObject prefab) where T : MonoBehaviour, IDespawned
        {
            T bullet = _prefabPoll.Spawn<T>(prefab);
            _activeObjects.Add(bullet);

            bullet.BackToPool += asd;

            return bullet;
        }

        private void asd(IDespawned bullet)
        {
            _activeObjects.Remove(bullet);
            bullet.BackToPool -= asd;
        }

        private void FixedUpdate()
        {
            Debug.Log(_activeObjects.Count);
        }
    }
}