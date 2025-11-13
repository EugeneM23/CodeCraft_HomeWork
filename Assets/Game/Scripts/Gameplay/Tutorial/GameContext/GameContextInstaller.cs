using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using Modules.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    public class GameContextInstaller : SceneContextInstaller<IGameContext>
    {
        [SerializeField] private SceneEntity _character;
        [SerializeField] private Camera _camera;
        [SerializeField] private int _cameraSpeed;
        [SerializeField] private Joystick _movementJoystick;

        protected override void Install(IGameContext context)
        {
            Vector3 offset = _camera.transform.position - _character.transform.position;

            context.AddCharacter(_character);
            context.AddCameraOffset(new Const<Vector3>(offset));
            context.AddCamera(_camera);
            context.AddCameraSpeed(new BaseVariable<int>(_cameraSpeed));
            context.AddMoveJoystick(_movementJoystick);

            context.AddController<CameraFollowController>();
            context.AddController<CharacterMoveController>();
            context.AddController<CharacterFireController>();
        }
    }
}