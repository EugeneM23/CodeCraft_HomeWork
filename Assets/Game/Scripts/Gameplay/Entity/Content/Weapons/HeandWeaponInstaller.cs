using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class HeandWeaponInstaller : SceneEntityInstaller
    {
        [SerializeField] private WeaponID _id;
        [SerializeField] private RuntimeAnimatorController _animController;

        public override void Install(IEntity entity)
        {
            entity.AddAmmo(new Ammo(20));

            entity.AddFireAction(new BaseAction(() =>
                GameContext.Instance.GetPlayers()[0].GetCharacter().GetAnimator().SetTrigger("Attack")));
            
            entity.AddFireCondition(new AndExpression(() => true));
            entity.AddAnimationController(_animController);
            entity.AddWeaponId(_id);
            entity.AddTransform(transform);
        }
    }
}