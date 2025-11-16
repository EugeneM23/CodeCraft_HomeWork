using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using UnityEngine;

namespace Game
{
    public class WeaponInstaller : SceneEntityInstaller
    {
        [SerializeField] private SceneEntity _pickup;
        [SerializeField] private int _damage;
        [SerializeField] private int _ammo = 10;
        [SerializeField] private Transform _firePoint;

        public override void Install(IEntity entity)
        {
            entity.AddTransform(transform);
            entity.AddDamage(new Const<int>(_damage));
            entity.SetFirePoint(_firePoint);
            entity.AddFireAction(new WeaponFireAction(entity));
            entity.AddFireEvent(new BaseEvent());
        }
    }
}