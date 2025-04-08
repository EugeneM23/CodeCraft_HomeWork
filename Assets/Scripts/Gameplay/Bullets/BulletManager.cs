using System.Collections.Generic;
using Modules.Pooling;
using UnityEngine;

namespace Gameplay
{
    public class BulletManager : MonoBehaviour
    {
        [SerializeField] private LevelBounds levelBounds;
        [SerializeField] private PrefabPool _prefabPool;

        private readonly List<GameObject> _activeObjects = new();

        private void FixedUpdate()
        {
            for (int i = 0; i < _activeObjects.Count; i++)
                if (!levelBounds.InBounds(_activeObjects[i].transform.position))
                    _activeObjects[i].GetComponent<IDespawned>().Destroy();
        }

        public void SpawnBullet(BulletInfo info, Vector3 firePointPosition, Vector3 fireDirection)
        {
            Bullet bullet = _prefabPool.Spawn<Bullet>(info.BulletPrefab);
            _activeObjects.Add(bullet.gameObject);

            bullet.GetComponent<IDespawned>().DeSpawn += RemoveBullet;
            bullet.SetMoveDirection(fireDirection);
            bullet.SetPosition(firePointPosition);
            bullet.Construct(info.Damage, info.BulletColor, info.PhysicsLayer, info.BulletSpeed);
        }

        private void RemoveBullet(GameObject bullet)
        {
            _activeObjects.Remove(bullet);
            _prefabPool.DeSpawn(bullet);
            bullet.GetComponent<IDespawned>().DeSpawn -= RemoveBullet;
        }
    }
}