using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
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

            ability.AddTargetCondition(new InlineFunction<IEntity, bool>(target =>
                target != null && ManaUseCase.Enough(context, _manaCost)));

            // The stun itself lives in the action, behind the condition. It used to be applied by
            // the input handler next to AbilityUseCase.Use, which returns void: the mana check
            // failed silently and the target was stunned anyway, so the ability was free.
            ability.AddTargetAction(new InlineAction<IEntity>(target =>
            {
                if (!ManaUseCase.Spend(context, _manaCost))
                    return;

                BuffUseCase.Apply(target, _stunBuff.CreateBuff());
            }));

            ability.AddTargetEvent(new Atomic.Elements.Event<IEntity>());
            ability.AddManaCost(new Const<int>(_manaCost));

            ability.WhenTick(dt =>
            {
                if (Input.GetKeyDown(_abilityKey))
                {
                    if (RayCastUseCase.RayCastTarget(out var target, _layerMask))
                        AbilityUseCase.Use(ability, target);
                }
            });
        }
    }
}