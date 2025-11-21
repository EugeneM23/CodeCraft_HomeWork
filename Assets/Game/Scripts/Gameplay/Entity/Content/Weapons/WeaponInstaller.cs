using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class WeaponInstaller : SceneEntityInstaller
    {
        [SerializeField] private WeaponID _id;
        [SerializeField] private Cooldown _fireRate;
        [SerializeField] private int _damage;
        [SerializeField] private int _ammo;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private RuntimeAnimatorController _animController;

        public override void Install(IEntity entity)
        {
            entity.AddRangeWeaponTag();
            
            entity.AddAnimationController(_animController);
            entity.AddWeaponId(_id);
            entity.AddAmmo(new Ammo(_ammo));
            entity.AddDamage(new Const<int>(_damage));
            entity.AddWeaponFireRate(_fireRate);
            entity.AddTransform(transform);
            entity.AddMoveCondition(new AndExpression((() => true)));

            entity.SetFirePoint(_firePoint);
            entity.AddFireEvent(new BaseEvent());
            entity.AddFireAction(new WeaponFireAction(entity));

            entity.AddFireCondition(new AndExpression());
            entity.GetFireCondition().Append(() => entity.GetAmmo().GetCount() > 0);
            entity.GetFireCondition().Append(entity.GetWeaponFireRate().IsExpired);

            entity.AddBehaviour(new WeaponCooldownBehaviour());
        }
    }
}