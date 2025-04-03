using System.Collections.Generic;
using Modules.PrefabPool;
using UnityEngine;

namespace Gameplay
{
    public class Bulletmanager : MonoBehaviour
    {
        [SerializeField] private LevelBounds levelBounds;

        private PrefabPool _prefabPoll;
        private List<GameObject> _activeObjects = new List<GameObject>();

        private void Awake()
        {
            _prefabPoll = new PrefabPool();
        }

        /*public T GetBullet<T>(GameObject prefab) where T : MonoBehaviour, IDespawned
        {
            T bullet = _prefabPoll.Spawn<T>(prefab);
            _activeObjects.Add(bullet.gameObject);

            bullet.DeSpawn += RemoveBulletFromActivList;

            return bullet;
        }*/

        private void RemoveBulletFromActivList(GameObject bullet)
        {
            _activeObjects.Remove(bullet);
            bullet.GetComponent<IDespawned>().DeSpawn -= RemoveBulletFromActivList;
        }

        private void FixedUpdate()
        {
            for (int i = 0; i < _activeObjects.Count; i++)
            {
                if (!levelBounds.InBounds(_activeObjects[i].transform.position))
                {
                    _activeObjects[i].GetComponent<IDespawned>().Destroy();
                }
            }
        }

        public void SpawnBullet(BulletInfo info, Vector3 firePointPosition, Vector3 fireDirection)
        {
            Bullet bullet = _prefabPoll.Spawn<Bullet>(info.BulletPrefab);
            _activeObjects.Add(bullet.gameObject);
            bullet.GetComponent<IDespawned>().DeSpawn += RemoveBulletFromActivList;
            
            bullet.SetMoveDirection(fireDirection);
            bullet.SetPosition(firePointPosition);
            bullet.Construct(info.Damage, info.BulletColor, info.PhysicsLayer, info.BulletSpeed);
        }
    }
}