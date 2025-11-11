using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game
{
    public static class FireUseCase
    {
        public static IEntity Fire(this IWeaponEntity entity)
        {
            var bulletPrefab = entity.GetBulletPrefab(); 
            Transform firePoint = entity.GetFirePoint();

             return SceneEntity.Instantiate(bulletPrefab, firePoint.position, firePoint.rotation); 
        }
    }
}