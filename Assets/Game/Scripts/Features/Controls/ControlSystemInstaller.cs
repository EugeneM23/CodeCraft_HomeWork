using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The player's controls on the player context: the Character slot — the tank the player
    /// drives — and every controller that reads the keys and the mouse and drives what is in it.
    /// </summary>
    [Serializable]
    public class ControlSystemInstaller : IEntityInstaller<IPlayerContext>
    {
        [SerializeField] private MonoEntity _character;

        public void Install(IPlayerContext context)
        {
            context.AddCharacter(new ReactiveVariable<IEntity>(_character));

            // The one place the controls are read. The Input System replaces this line.
            context.AddPlayerInput(new LegacyPlayerInput());

            // First: the vehicle in the slot gets the player as its driver and the Player tag.
            context.AddBehaviour<PlayerVehicleController>();

            context.AddBehaviour<ControllableDriveController>();
            context.AddBehaviour<PlayerAimController>();
            context.AddBehaviour<ControllableFireController>();
        }
    }
}
