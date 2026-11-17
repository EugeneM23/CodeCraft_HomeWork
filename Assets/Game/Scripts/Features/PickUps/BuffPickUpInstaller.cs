using Atomic.Elements;
using Atomic.Entities;
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

            PickUpUseCase.AddPickUp(entity, transform,
                character =>
                {
                    BuffUseCase.Apply(character, buffConfig.CreateBuff());
                    return true;
                },
                () => GameContext.Instance.GetGameFactory().Destroy(entity));
        }
    }
}