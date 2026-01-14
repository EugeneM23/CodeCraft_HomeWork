using Atomic.Elements;
using Atomic.Entities;
using Game.Content;
using UnityEngine;

namespace Game
{
    public class DashPickUpInstaller : SceneEntityInstaller
    {
        public override void Install(IEntity entity)
        {
            GameContext gameContext = GameContext.Instance;

            entity.AddTransform(transform);
            entity.AddInteractableTag();
            entity.AddPickUpEvent(new BaseEvent());

            entity.AddInteractAction(
                new BaseAction<IEntity>(character =>
                {
                    IPlayerContext playerContext = PlayerUseCase.GetPlayerContext(gameContext, character);

                    //Костыль!
                    IReactiveDictionary<string, Ability> dictionary = playerContext.GetAbilities();
                    foreach (var (key, ability) in dictionary)
                    {
                        if (key == nameof(DashAbilityConfig))
                            ability.GetCharges().Value++;
                    }

                    entity.GetPickUpEvent().Invoke();
                    gameObject.SetActive(false);
                }));
        }
    }
}