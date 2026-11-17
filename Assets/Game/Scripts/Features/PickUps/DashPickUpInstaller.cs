using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class DashPickUpInstaller : MonoEntityInstaller
    {
        public override void Install(IEntity entity)
        {
            GameContext gameContext = GameContext.Instance;

            PickUpUseCase.AddPickUp(entity, transform,
                character =>
                {
                    IPlayerContext playerContext = gameContext.GetPlayerContext();

                    //Костыль!
                    IReactiveDictionary<string, Ability> dictionary = playerContext.GetAbilities();
                    foreach (var (key, ability) in dictionary)
                    {
                        if (key == nameof(DashAbilityConfig))
                            ability.GetCharges().Value++;
                    }

                    return true;
                },
                () => gameObject.SetActive(false));
        }
    }
}