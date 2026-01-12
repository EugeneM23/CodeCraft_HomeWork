using Atomic.Elements;
using UnityEngine;

namespace Game.Content
{
    [CreateAssetMenu(menuName = "AbilitySystem/DashAbility", fileName = "DashAbilityConfig", order = 0)]
    public class DashAbilityConfig : AbilityConfig
    {
        [SerializeField] private int _initialCharges;
        [SerializeField] private float _multiplier = 2;

        protected override void Install(Ability ability, IPlayerContext context)
        {
            ability.AddBaseAbilityTag();
            ability.AddBaseCondition(new BaseFunction<bool>(() => ability.GetCharges().Value > 0));
            ability.AddBaseAction(new BaseAction(() =>
            {
                Debug.Log("Dash" + context.GetCharacter().Value.GetMoveSpeed().Value);
                ability.GetCharges().Value--;
                context.GetCharacter().Value.GetMoveSpeed().Value *= _multiplier;
            }));
            ability.AddBaseEvent(new BaseEvent());

            ability.AddCharges(new ReactiveInt(_initialCharges));
        }
    }
}