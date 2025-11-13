using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class GameContextInstaller : SceneContextInstaller<IGameContext>
    {
        [SerializeField] private SceneEntity _character;
        [SerializeField] private UnityEngine.Camera _camera;

        protected override void Install(IGameContext context)
        {
            Vector3 offset = _camera.transform.position - _character.transform.position;

            context.AddCharacter(_character);
            context.AddCameraOffset(new Const<Vector3>(offset));
            context.AddCamera(_camera);

            context.AddController<CameraFollowController>();
        }
    }
}