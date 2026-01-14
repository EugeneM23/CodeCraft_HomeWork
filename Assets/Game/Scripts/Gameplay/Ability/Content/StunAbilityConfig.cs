using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using UnityEngine;

namespace Game.Content
{
    [CreateAssetMenu(menuName = "AbilitySystem/StunAbilityConfig", fileName = "StunAbilityConfig", order = 0)]
    public class StunAbilityConfig : AbilityConfig
    {
        [SerializeField] private int _initialCharges;
        [SerializeField] private int _manaCost = 2;
        [SerializeField] private KeyCode _abilityKey = KeyCode.R;
        [SerializeField] private LayerMask _layerMask;
        [SerializeField] private BuffConfig _stunBuff;

        protected override void Install(Ability ability, IPlayerContext context)
        {
            ability.AddTargetAbilityTag();

            ability.AddTargetCondition(new BaseFunction<IEntity, bool>(point =>
                ManaUseCase.Enough(context, _manaCost)));

            ability.AddTargetAction(new BaseAction<IEntity>(target => { ManaUseCase.Spend(context, _manaCost); }));

            ability.AddTargetEvent(new BaseEvent<IEntity>());
            ability.AddManaCost(new Const<int>(_manaCost));

            ability.WhenUpdate(dt =>
            {
                if (Input.GetKeyDown(_abilityKey))
                {
                    if (RayCastUseCase.RayCastTarget(out var target, _layerMask))
                    {
                        AbilityUseCase.Use(ability, target);
                        BuffUseCase.Apply(target, _stunBuff.CreateBuff());
                    }
                }
            });
        }
    }
}