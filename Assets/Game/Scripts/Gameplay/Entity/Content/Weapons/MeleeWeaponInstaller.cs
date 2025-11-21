using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class MeleeWeaponInstaller : SceneEntityInstaller
    {
        [SerializeField] private WeaponID _id;
        [SerializeField] private RuntimeAnimatorController _animController;
        [SerializeField] private Cooldown _fireRate;

        public override void Install(IEntity entity)
        {
            //Core
            entity.AddWeaponId(_id);
            entity.AddAnimationController(_animController);
            entity.AddTransform(transform);

            //Attack
            entity.AddMoveCondition(new AndExpression());
            entity.GetMoveCondition().Append(_fireRate.IsExpired);

            entity.AddWeaponFireRate(_fireRate);
            entity.AddFireAction(new MeleeWeaponFireAction(_fireRate));
            entity.AddFireCondition(new AndExpression(() => true));

            entity.AddBehaviour(new WeaponCooldownBehaviour());
        }
    }
}