using Atomic.Contexts;
using Modules.Common;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public class PlayerContextInstaller : SceneContextInstaller<IPlayerContext>
    {
        [SerializeField] private PlayerID _playerID;
        [SerializeField] private Joystick _movementJoystick;
        [SerializeField] private Joystick _rotationJoystick;
        [SerializeField] private CharacterSystemInstaller _characterSystem;
        [SerializeField] private CameraSystemInstaller _cameraSystem;

        protected override void Install(IPlayerContext context)
        {
            GameContext.Instance.GetPlayers().Add(_playerID, context);

            _characterSystem.Install(context);
            _cameraSystem.Install(context);

            context.AddMoveJoystick(_movementJoystick);
            context.AddRotateJoystick(_rotationJoystick);
        }
    }
}