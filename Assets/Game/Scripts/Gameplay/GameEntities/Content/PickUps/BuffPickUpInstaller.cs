using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public class BuffPickUpInstaller : MonoEntityInstaller
    {
        [SerializeField] private BuffConfig buffConfig;

        public override void Install(IEntity entity)
        {
            entity.AddEntityID(gameObject.name.Replace("(Clone)", ""));
            entity.AddTransform(transform);
            entity.AddInteractableTag();
            entity.AddPickUpEvent(new Atomic.Elements.Event());

            entity.AddInteractAction(new InlineAction<IEntity>(character =>
            {
                entity.GetPickUpEvent().Invoke();
                BuffUseCase.Apply(character, buffConfig.CreateBuff());
                GameContext.Instance.GetGameFactory().Destroy(entity);
            }));
        }
    }
}