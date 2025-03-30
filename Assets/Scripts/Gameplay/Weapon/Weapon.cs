using System.Collections;
using Modules.Pools;
using UnityEngine;

namespace Gameplay
{
    public class Weapon : MonoBehaviour
    {
        [SerializeField] private Transform _firePoint;
        [SerializeField] private int _damage;
        [SerializeField] private float _bulletSpeed;
        [SerializeField] private Color _bulletColor;
        [SerializeField] private PhysicsLayer _physicsLayer;
        [SerializeField] private float _fireRate;
        [SerializeField] private Bullet _bullet;

        private DefaultPool<Bullet> BulletPool;
        private bool _canFire = true;

        public void OnEnable() => _canFire = true;

        public void Shoot(Vector3 fireDirection)
        {
            if (!_canFire)
                return;
            StartCoroutine(Cooldown());
            SetupBullet(fireDirection);
        }

        private void SetupBullet(Vector3 fireDirection)
        {
            Debug.Log(_damage == null);
            var bullet = BulletPool.Rent();
            bullet.gameObject.transform.position = _firePoint.position;
            bullet.Construct(_damage, _bulletColor, _physicsLayer);
            bullet.SetVelocity(fireDirection * _bulletSpeed);
        }

        public void SetPool(DefaultPool<Bullet> pool) => BulletPool = pool;

        private IEnumerator Cooldown()
        {
            _canFire = false;
            yield return new WaitForSeconds(_fireRate);
            _canFire = true;
        }
    }
}