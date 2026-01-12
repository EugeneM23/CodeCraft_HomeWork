using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Content
{
    [CreateAssetMenu(menuName = "AbilitySystem/DashAbility", fileName = "DashAbilityConfig", order = 0)]
    public class DashAbilityConfig : AbilityConfig
    {
        [SerializeField] private int _initialCharges;
        [SerializeField] private float _multiplier = 2;
        [SerializeField] private KeyCode _dashKey = KeyCode.Space;

        protected override void Install(Ability ability, IPlayerContext context)
        {
            ability.AddBaseAbilityTag();
            ability.AddBaseCondition(new BaseFunction<bool>(() => ability.GetCharges().Value > 0));

            ability.AddBaseAction(new BaseAction(() =>
            {
                ability.GetCharges().Value--;
                context.GetCharacter().Value.GetMoveSpeed().Value *= _multiplier;
            }));

            ability.AddBaseEvent(new BaseEvent());

            ability.AddCharges(new ReactiveInt(_initialCharges));

            ability.WhenUpdate(dt =>
            {
                if (Input.GetKeyDown(_dashKey))
                    AbilityUseCase.Use(ability);
            });
        }
    }
}