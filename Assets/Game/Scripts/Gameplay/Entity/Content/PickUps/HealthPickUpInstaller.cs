using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public class HealthPickUpInstaller : SceneEntityInstaller
    {
        [SerializeField] private int _healthAmount = 100;
        [SerializeField] private GameObject _interactUI;

        public override void Install(IEntity entity)
        {
            entity.AddShowUIAction(new BaseAction<bool>((show) =>
            {
                _interactUI.SetActive(show);
                entity.GetIsInteract().Value = show;
            }));
            entity.AddIsInteract(new ReactiveBool(false));
            entity.AddUITransform(_interactUI.transform);

            entity.AddTransform(transform);
            entity.AddInteractableTag();

            entity.AddInteractAction(
                new BaseAction<IEntity>((character =>
                {
                    character.GetHealth().Add(_healthAmount);
                    gameObject.SetActive(false);
                })));

        }
    }
}