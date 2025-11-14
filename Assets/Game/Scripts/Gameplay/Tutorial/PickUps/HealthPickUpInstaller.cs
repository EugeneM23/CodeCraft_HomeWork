using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public class HealthPickUpInstaller : SceneEntityInstaller
    {
        [SerializeField] private int _healthAmount = 100;

        public override void Install(IEntity entity)
        {
            entity.AddInteractableTag();

            entity.AddInteractAction(
                new BaseAction<IEntity>((character =>
                {
                    character.GetHealth().Value += _healthAmount;
                    gameObject.SetActive(false);
                })));
        }
    }
}