using Atomic.Entities;

namespace Game.Gameplay
{
    public class FireCameraShakeBehaviour : IEntityInit
    {
        private readonly IEntity _camera;
        private CameraShakeArgs _cameraShakeArgs;

        public FireCameraShakeBehaviour(IEntity camera)
        {
            _camera = camera;
        }

        public void Init(in IEntity entity)
        {
            entity.GetFireEvent().Subscribe(CameraShake);
            _cameraShakeArgs = entity.GetCameraShakeArgs();
        }

        private void CameraShake()
        {
            _camera.GetCameraShakeEvent().Invoke(_cameraShakeArgs);
        }
    }
}