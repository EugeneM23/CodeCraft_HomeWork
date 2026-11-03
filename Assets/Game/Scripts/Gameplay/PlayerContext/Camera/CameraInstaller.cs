using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    public class CameraInstaller : MonoEntityInstaller
    {
        public override void Install(IEntity entity)
        {
            entity.AddTransform(transform);
            entity.AddCameraShakeEvent(new Atomic.Elements.Event<CameraShakeArgs>());

            entity.AddBehaviour<CameraShakeBehaviour>();
        }
    }
}