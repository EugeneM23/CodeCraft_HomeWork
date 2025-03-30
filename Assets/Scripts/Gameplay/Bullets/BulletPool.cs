using Modules.Pools;

namespace Gameplay
{
    public class BulletPool : DefaultPool<Bullet>
    {
        protected override void OnCreate(Bullet enemy) => enemy.SetPull(this);

        private void Update()
        {
            for (int i = 0; i < _activeObjects.Count - 1; i++)
            {
                if (!LevelBounds.Bounds.InBounds(_activeObjects[i].transform.position))
                {
                    var asd = _activeObjects[i];
                    _activeObjects[i].Destroy();
                    _activeObjects.Remove(asd);
                }
            }
        }
    }
}