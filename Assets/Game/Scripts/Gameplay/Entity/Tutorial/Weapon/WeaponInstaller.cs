using Atomic.Elements;
using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game
{
    public class WeaponInstaller : SceneEntityInstaller
    {
        [SerializeField] private int _damage;
        [SerializeField] private SceneEntity _bulletPrefab;
        [SerializeField] private Transform _firePoint;

        public override void Install(IEntity entity)
        {
            entity.AddDamage(new Const<int>(_damage));
            entity.SetBulletPrefab(_bulletPrefab);
            entity.SetFirePoint(_firePoint);
        }
    }
}