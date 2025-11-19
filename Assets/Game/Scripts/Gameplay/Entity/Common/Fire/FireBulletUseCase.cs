using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class FireBulletUseCase
    {
        public static IEntity SpawnBullet(IEntity weapon, IGameContext gameContext, Transform firePoint)
        {
            IEntity bullet = gameContext.GetBulletPool().Rent();

            bullet.AddDamage(weapon.GetDamage());
            bullet.GetTransform().SetPositionAndRotation(firePoint.position, firePoint.rotation);
            bullet.GetMoveDirection().Value = firePoint.forward;
            bullet.GetLifeTime().Reset();

            return bullet;
        }

        public static void UnSpawnBullet(in IGameContext gameContext, in IEntity bullet)
        {
            gameContext.GetBulletPool().Return(bullet);
        }
    }
}