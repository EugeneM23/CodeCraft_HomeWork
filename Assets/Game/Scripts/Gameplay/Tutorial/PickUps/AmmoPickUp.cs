using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class AmmoPickUpInstaller : SceneEntityInstaller
    {
        public override void Install(IEntity entity)
        {
            entity.AddInteractableTag();
            entity.AddInteractAction(
                new BaseAction<IEntity>((character =>
                {
                    Debug.Log("AmmoPickUpInstaller install");
                    IEntity weapon = character.GetWeapon();
                    if (weapon == null) return;

                    if (weapon.Trygetamm)
                    {
                        
                    }
                })));
        }
    }
}