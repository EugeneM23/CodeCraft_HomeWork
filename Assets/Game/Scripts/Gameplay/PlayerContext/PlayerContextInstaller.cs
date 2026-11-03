using Atomic.Elements;
using Atomic.Entities;
using Modules.Common;
using UnityEngine;

namespace Game
{
    public class PlayerContextInstaller : MonoEntityInstaller<PlayerContext>
    {
        [SerializeField] private PlayerID _playerID;
        [SerializeField] private MonoEntity _playerEntity;
        [SerializeField] private Joystick _movementJoystick;
        [SerializeField] private Joystick _rotationJoystick;
        [SerializeField] private CharacterSystemInstaller _characterSystem;
        [SerializeField] private CameraSystemInstaller _cameraSystem;
        [SerializeField] private AbilitySystemInstaller _abilitySystem;
        [SerializeField] private ManaSystemInstaller _manaSystem;

        public override void Install(PlayerContext context)
        {
            _characterSystem.Install(context);
            _cameraSystem.Install(context);
            _abilitySystem.Install(context);
            _manaSystem.Install(context);

            // CharacterSystemInstaller already added Character above. Under the old API the
            // second AddValue returned false and did nothing; the new one throws on a
            // duplicate key, so the redundant add is dropped and the first one still wins.
            context.AddMoveJoystick(_movementJoystick);
            context.AddRotateJoystick(_rotationJoystick);
        }
    }
}