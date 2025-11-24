using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class HealthPickUpInstaller : SceneEntityInstaller
    {
        [SerializeField] private int _healthAmount = 100;

        public override void Install(IEntity entity)
        {
            entity.AddShowUIAction(new BaseAction<bool>((show) => { entity.GetIsInteract().Value = show; }));
            entity.AddIsInteract(new ReactiveBool(false));

            entity.AddTransform(transform);
            entity.AddInteractableTag();
            entity.AddPickUpEvent(new BaseEvent());

            entity.AddInteractAction(
                new BaseAction<IEntity>((character =>
                {
                    entity.GetPickUpEvent().Invoke();
                    character.GetHealth().Add(_healthAmount);
                    gameObject.SetActive(false);
                })));
        }
    }
}