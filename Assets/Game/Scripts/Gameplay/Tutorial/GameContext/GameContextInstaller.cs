using Atomic.Contexts;
using Modules.Common;
using UnityEngine;

namespace Game
{
    public class GameContextInstaller : SceneContextInstaller<IGameContext>
    {
        [SerializeField] private Joystick _movementJoystick;
        [SerializeField] private CharacterSystemInstaller _characterSystem;
        [SerializeField] private CameraSystemInstaller _cameraSystem;

        protected override void Install(IGameContext context)
        {
            _cameraSystem.Install(context);
            _characterSystem.Install(context);

            context.AddMoveJoystick(_movementJoystick);
        }
    }
}