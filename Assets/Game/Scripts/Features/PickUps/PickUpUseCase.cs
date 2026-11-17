using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class PickUpUseCase
    {
        /// <summary>
        /// What every pickup is: something the character can interact with that, when taken, hands
        /// over its content, raises PickUpEvent (the pick-up sound) and goes away.
        /// </summary>
        /// <param name="give">Hands the content to the character. False leaves the pickup where it
        /// is and silent — ammo with no weapon to load it into, grenades with no pouch for them.</param>
        /// <param name="remove">Takes the pickup out of the world: hidden, or back to its pool.</param>
        public static void AddPickUp(IEntity entity, Transform transform, Func<IEntity, bool> give, Action remove)
        {
            entity.AddTransform(transform);
            entity.AddInteractableTag();
            entity.AddPickUpEvent(new Atomic.Elements.Event());

            entity.AddInteractAction(new InlineAction<IEntity>(character =>
            {
                if (!give(character))
                    return;

                entity.GetPickUpEvent().Invoke();
                remove();
            }));
        }
    }
}
