using Atomic.Contexts;
using Game.Scripts.Gameplay.Tutorial.Bullet;
using Modules.Common;
using UnityEngine;

namespace Game
{
    public class GameContextInstaller : SceneContextInstaller<IGameContext>
    {
        [SerializeField] private Joystick _movementJoystick;
        [SerializeField] private CharacterSystemInstaller _characterSystem;
        [SerializeField] private CameraSystemInstaller _cameraSystem;
        [SerializeField] private BulletSystemInstaller _bulletInstaller;

        protected override void Install(IGameContext context)
        {
            _cameraSystem.Install(context);
            _characterSystem.Install(context);
            _bulletInstaller.Install(context);
            

            context.AddMoveJoystick(_movementJoystick);
        }
    }
}