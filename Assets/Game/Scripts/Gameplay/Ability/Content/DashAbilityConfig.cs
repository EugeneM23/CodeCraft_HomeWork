using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using UnityEngine;

namespace Game.Content
{
    [CreateAssetMenu(menuName = "AbilitySystem/DashAbility", fileName = "DashAbilityConfig", order = 0)]
    public class DashAbilityConfig : AbilityConfig
    {
        [SerializeField] private int _initialCharges;
        [SerializeField] private float _multiplier = 2;
        [SerializeField] private KeyCode _dashKey = KeyCode.LeftShift;
        [SerializeField] private BuffConfig _dashBuff;

        protected override void Install(Ability ability, IPlayerContext context)
        {
            ability.AddBaseAbilityTag();

            IEntity character = context.GetCharacter().Value;
            
            ability.AddBaseCondition(new InlineFunction<bool>(() =>
                ability.GetCharges().Value > 0 && BuffUseCase.CanApply(character, _dashBuff)));

            ability.AddBaseAction(new InlineAction(() =>
            {
                ability.GetCharges().Value--;
                BuffUseCase.Apply(character, _dashBuff.CreateBuff());
            }));

            ability.AddBaseEvent(new Atomic.Elements.Event());
            ability.AddCharges(new ReactiveInt(_initialCharges));
            
            ability.WhenTick(dt =>
            {
                if (Input.GetKeyDown(_dashKey))
                    AbilityUseCase.Use(ability);
            });
        }
    }
}