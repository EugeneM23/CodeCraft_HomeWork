using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class AmmoPickUpInstaller : MonoEntityInstaller
    {
        [SerializeField] private int _ammoAmount;

        public override void Install(IEntity entity)
        {
            PickUpUseCase.AddPickUp(entity, transform,
                character =>
                {
                    IEntity weapon = character.GetWeapon().Value;
                    if (weapon == null || !weapon.TryGetAmmo(out Ammo ammo)) return false;

                    ammo.Add(_ammoAmount);
                    return true;
                },
                () => gameObject.SetActive(false));
        }
    }
}