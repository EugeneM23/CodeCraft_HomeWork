using Atomic.Elements;
using Atomic.Entities;

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
                    playerContext.GetDashAbility().GetCharges().Value++;
                    entity.GetPickUpEvent().Invoke();
                    gameObject.SetActive(false);
                }));
        }
    }
}