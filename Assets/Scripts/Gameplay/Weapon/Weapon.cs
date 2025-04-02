using System.Collections;
using NewPool;
using UnityEngine;

namespace Gameplay
{
    public class Weapon : MonoBehaviour
    {
        [SerializeField] private WeaponData _wd;
        [SerializeField] private Transform _firePoint;


        private bool _canFire = true;

        public void OnEnable()
        {
            _canFire = true;
        }

        public void Shoot(Vector3 fireDirection)
        {
            if (!_canFire)
                return;

            StartCoroutine(Cooldown());
            SetupBullet(fireDirection);
        }

        private void SetupBullet(Vector3 fireDirection)
        {
            Bullet go = PrefabPool.Instance.Spawn<Bullet>(_wd.BulletPrefab);

            Bullet bullet = go.GetComponent<Bullet>();
            bullet.SetMoveDirection(fireDirection);
            bullet.gameObject.transform.position = _firePoint.position;
            bullet.Construct(_wd.Damage, _wd.BulletColor, _wd.PhysicsLayer);
        }

        public void SetWeaponData(WeaponData data) => _wd = data;

        private IEnumerator Cooldown()
        {
            _canFire = false;
            yield return new WaitForSeconds(_wd.FireRate);
            _canFire = true;
        }
    }
}