using System;
using Atomic.Contexts;
using Atomic.Entities;
using UnityEngine;

namespace Game.Scripts.Gameplay.Tutorial.Bullet
{
    [Serializable]
    public class BulletSystemInstaller : IContextInstaller<IGameContext>
    {
        [SerializeField] private SceneEntity _bulletPrefab;

        public void Install(IGameContext context)
        {
            GameObject poolRoot = new GameObject("BulletPool");
            context.AddBulletPool(new SceneEntityPool(_bulletPrefab, poolRoot.transform));
        }
    }
}