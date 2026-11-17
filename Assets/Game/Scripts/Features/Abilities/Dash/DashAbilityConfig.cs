using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "AbilitySystem/DashAbility", fileName = "DashAbilityConfig", order = 0)]
    public class DashAbilityConfig : AbilityConfig
    {
        [SerializeField] private int _initialCharges;
        [SerializeField] private KeyCode _dashKey = KeyCode.LeftShift;
        [SerializeField] private BuffConfig _dashBuff;

        protected override void Install(Ability ability, IPlayerContext context)
        {
            ability.AddBaseAbilityTag();

            // The character is read inside the closures, not captured at Install: Character is a
            // reactive variable precisely because the entity is replaceable, and a captured one
            // means the dash keeps buffing the body left behind by the last respawn.
            ability.AddBaseCondition(new InlineFunction<bool>(() =>
            {
                IEntity character = context.GetCharacter().Value;
                // Not from inside a vehicle: the dash is a buff on a body on foot.
                return character != null
                       && !character.HasVehicleTag()
                       && ability.GetCharges().Value > 0
                       && BuffUseCase.CanApply(character, _dashBuff);
            }));

            ability.AddBaseAction(new InlineAction(() =>
            {
                IEntity character = context.GetCharacter().Value;
                if (character == null)
                    return;

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