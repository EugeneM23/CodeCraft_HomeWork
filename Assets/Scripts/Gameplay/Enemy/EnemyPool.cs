using Modules.Pools;

namespace Gameplay
{
    public class EnemyPool : DefaultPool<Enemy>
    {
        protected override void OnCreate(Enemy obj) => obj.SetPull(this);
    }
}