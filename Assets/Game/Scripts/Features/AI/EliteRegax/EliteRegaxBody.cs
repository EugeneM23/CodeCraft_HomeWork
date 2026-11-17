using System;
using AIKit;
using AIKit.NavMeshNavigation;
using Atomic.Entities;
using Game.Horde;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The elite Regax's body for its <see cref="AICharacter"/> (picked in its Body section): the gamepad is the Atomic
    /// walker (<see cref="AtomicActuator"/>), the way is found on the level's voxel grid (<see cref="VoxelGridNavigator"/>),
    /// the eyes are AIKit's physics eyes (<see cref="PhysicsEyes3D"/>: lines of sight blocked by the ground and the
    /// houses) and the only thing they look for is the player's vehicle (<see cref="PlayerTankScanner"/>). The parts are
    /// made when the character attaches the body (its Awake); the entity, the grid and the tank come later, when the
    /// entity starts (<see cref="Bind"/>).
    /// </summary>
    [Serializable]
    public sealed class EliteRegaxBody : AgentBody
    {
        [Tooltip("What blocks the eyes (and is ground to them): the ground and the houses.")]
        public LayerMask SightBlockers = (1 << HordeNavGridSettings.LAYER_GROUND) | (1 << HordeNavGridSettings.LAYER_DEFAULT);

        [NonSerialized] private EliteMotor _motor;
        [NonSerialized] private AtomicActuator _actuator;
        [NonSerialized] private VoxelGridNavigator _navigator;
        [NonSerialized] private PhysicsEyes3D _eyes;
        [NonSerialized] private PlayerTankScanner _scanner;

        public EliteMotor Motor => _motor;

        public AtomicActuator Gamepad => _actuator;

        public VoxelGridNavigator Pathing => _navigator;

        public PlayerTankScanner TankScanner => _scanner;

        public override void Attach(Transform owner)
        {
            _motor = new EliteMotor(owner);
            _actuator = new AtomicActuator(owner, _motor);
            _navigator = new VoxelGridNavigator(_actuator);
            _eyes = new PhysicsEyes3D(owner, new NavMeshBody
            {
                SightBlockers = SightBlockers,
                GroundLayers = SightBlockers,
                TargetLayers = 0
            });
            _scanner = new PlayerTankScanner();
        }

        /// <summary>Gives the parts what they work on: the walker's values, the level's grid and the player's vehicle.</summary>
        public void Bind(IEntity entity, LevelNav nav, PlayerTank tank)
        {
            if (entity != null)
                _actuator.Bind(entity);
            _motor.Bind(nav, tank);
            _navigator.Bind(nav);
            _scanner.Bind(tank);
        }

        public override IAgentActuator Actuator => _actuator;

        public override INavigator Navigator => _navigator;

        public override ISightProbe Sight => _eyes;

        public override IGroundProbe Ground => _eyes;

        public override ITargetScanner Scanner => _scanner;
    }
}
