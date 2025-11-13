using Atomic.Contexts;
using Game.Scripts.Gameplay.Tutorial.Bullet;
using Modules.Common;
using UnityEngine;

namespace Game
{
    public class GameContextInstaller : SceneContextInstaller<IGameContext>
    {
       
        [SerializeField] private BulletSystemInstaller _bulletInstaller;

        protected override void Install(IGameContext context)
        {
            _bulletInstaller.Install(context);

        }
    }
}