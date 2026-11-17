using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class HealthPickUpInstaller : MonoEntityInstaller
    {
        [SerializeField] private int _healthAmount = 100;

        public override void Install(IEntity entity)
        {
            PickUpUseCase.AddPickUp(entity, transform,
                character =>
                {
                    character.GetHealth().Add(_healthAmount);
                    return true;
                },
                () => gameObject.SetActive(false));
        }
    }
}