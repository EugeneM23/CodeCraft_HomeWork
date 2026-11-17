using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The player's controls on the player context: the Character slot — the body or the vehicle
    /// the player drives right now — and every controller that reads the keys and the mouse and
    /// drives what is in that slot.
    /// </summary>
    /// <remarks>
    /// The prefix of a controller says what it drives: Player* — anything in the slot, a body or a
    /// vehicle; PlayerCharacter* — the body on foot only; Controllable* — vehicles only.
    /// </remarks>
    [Serializable]
    public class ControlSystemInstaller : IEntityInstaller<IPlayerContext>
    {
        [SerializeField] private MonoEntity _character;

        public void Install(IPlayerContext context)
        {
            context.AddCharacter(new ReactiveVariable<IEntity>(_character));

            context.AddBehaviour<PlayerMoveController>();
            context.AddBehaviour<ControllableDriveController>();
            context.AddBehaviour<PlayerAimController>();
            context.AddBehaviour<PlayerCharacterFireController>();
            context.AddBehaviour<ControllableFireController>();
            context.AddBehaviour<PlayerCharacterGrenadeController>();
            context.AddBehaviour<PlayerCharacterJumpController>();
            context.AddBehaviour<PlayerInteractController>();
            context.AddBehaviour<PlayerCharacterDropWeaponController>();
            context.AddBehaviour<PlayerCharacterDeathController>();
        }
    }
}
