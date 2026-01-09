using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using UnityEngine;

namespace Game
{
    public class BuffPickUpInstaller : SceneEntityInstaller
    {
        [SerializeField] private BaseBuff _buff;

        public override void Install(IEntity entity)
        {
            entity.AddEntityID(gameObject.name.Replace("(Clone)", ""));
            entity.AddTransform(transform);
            entity.AddInteractableTag();
            entity.AddPickUpEvent(new BaseEvent());

            entity.AddInteractAction(new BaseAction<IEntity>(character =>
            {
                entity.GetPickUpEvent().Invoke();
                BuffUseCase.Apply(character, _buff);
                GameContext.Instance.GetGameFactory().Destroy(entity);
            }));
        }
    }
}