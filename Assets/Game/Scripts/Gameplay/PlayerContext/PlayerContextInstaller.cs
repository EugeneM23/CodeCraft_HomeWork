using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using Modules.Common;
using UnityEngine;

namespace Game
{
    public class PlayerContextInstaller : SceneContextInstaller<IPlayerContext>
    {
        [SerializeField] private PlayerID _playerID;
        [SerializeField] private SceneEntity _playerEntity;
        [SerializeField] private Joystick _movementJoystick;
        [SerializeField] private Joystick _rotationJoystick;
        [SerializeField] private CharacterSystemInstaller _characterSystem;
        [SerializeField] private CameraSystemInstaller _cameraSystem;
        [SerializeField] private AbilitySystemInstaller _abilitySystem;
        [SerializeField] private ManaSystemInstaller _manaSystem;

        protected override void Install(IPlayerContext context)
        {
            _characterSystem.Install(context);
            _cameraSystem.Install(context);
            _abilitySystem.Install(context);
            _manaSystem.Install(context);

            context.AddCharacter(new ReactiveVariable<IEntity>(_playerEntity));
            context.AddMoveJoystick(_movementJoystick);
            context.AddRotateJoystick(_rotationJoystick);
        }
    }
}