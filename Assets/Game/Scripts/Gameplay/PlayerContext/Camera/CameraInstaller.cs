using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    public class CameraInstaller : SceneEntityInstaller
    {
        public override void Install(IEntity entity)
        {
            entity.AddTransform(transform);
            entity.AddCameraShakeEvent(new BaseEvent());

            entity.AddBehaviour<CameraShakeBehaviour>();
        }
    }
}