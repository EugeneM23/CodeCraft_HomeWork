using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game
{
    public class WeaponInstaller : SceneEntityInstaller<IWeaponEntity>
    {
        [SerializeField] private SceneEntity _bulletPrefab;
        [SerializeField] private Transform _firePoint;

        protected override void Install(IWeaponEntity entity)
        {
            entity.SetBulletPrefab(_bulletPrefab);
            entity.SetFirePoint(_firePoint);
        }
    }
}