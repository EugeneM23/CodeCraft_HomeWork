using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    public class BulletInstaller : SceneEntityInstaller
    {
        public override void Install(IEntity entity)
        {
            entity.AddTransform(transform);
            entity.AddMoveSpeed(new Const<float>(15f));
            entity.AddMoveDirection(new ReactiveVector3(transform.forward));
            
            entity.WhenFixedUpdate(entity.MoveSelf);
        }
    }
}