using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A pickup that adds grenades of one type to the character's stock. A character that does not
    /// carry that type (no GrenadeThrowerInstaller, or no slot for it) leaves it lying.
    /// </summary>
    public sealed class GrenadePickUpInstaller : MonoEntityInstaller
    {
        [SerializeField] private GrenadeType _type = GrenadeType.Frag;
        [SerializeField, Min(1)] private int _amount = 2;

        public override void Install(IEntity entity)
        {
            PickUpUseCase.AddPickUp(entity, transform,
                character =>
                {
                    if (character == null || !character.TryGetGrenadeAmmo(out var stocks)) return false;
                    if (!stocks.TryGetValue(_type, out Ammo grenades)) return false;

                    grenades.Add(_amount);
                    return true;
                },
                () => gameObject.SetActive(false));
        }
    }
}
