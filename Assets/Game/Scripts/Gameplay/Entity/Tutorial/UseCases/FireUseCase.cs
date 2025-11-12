using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game
{
    public static class FireUseCase
    {
        public static IEntity Fire(this IEntity weapon)
        {
            SceneEntity bulletPrefab = weapon.GetBulletPrefab();
            Transform firePoint = weapon.GetFirePoint();

            SceneEntity bullet = SceneEntity.Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
            bullet.AddDamage(weapon.GetDamage());
            
            return bullet;
        }
    }
}