using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class RangeWeaponInstaller : SceneEntityInstaller
    {
        [SerializeField] private WeaponID _id;
        [SerializeField] private Cooldown _fireRate;
        [SerializeField] private int _damage;
        [SerializeField] private int _ammo;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private RuntimeAnimatorController _animController;

        public override void Install(IEntity entity)
        {
            //Core
            entity.AddWeaponId(_id);
            entity.AddRangeWeaponTag();
            entity.SetFirePoint(_firePoint);
            entity.AddAmmo(new Ammo(_ammo));

            //Animation
            entity.AddAnimationController(_animController);
            entity.AddTransform(transform);

            //Movement
            entity.AddMoveCondition(new AndExpression((() => true)));

            //Fire
            entity.AddDamage(new Const<int>(_damage));
            entity.AddWeaponCooldown(_fireRate);
            entity.AddFireEvent(new BaseEvent());
            entity.AddFireAction(new RangeWeaponFireAction(entity));
            entity.AddFireCondition(new AndExpression());
            entity.GetFireCondition().Append(() => entity.GetAmmo().GetCount() > 0);
            entity.GetFireCondition().Append(entity.GetWeaponCooldown().IsExpired);

            entity.OnUpdated += deltaTime => entity.GetWeaponCooldown().Tick(deltaTime);

            //IK
            entity.AddIsIKEnable(new ReactiveBool(true));
        }
    }
}