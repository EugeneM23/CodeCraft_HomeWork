using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game
{
    public static class FireUseCase
    {
        public static IEntity Fire(IEntity weapon, in IGameContext gameContext)
        {
            Transform firePoint = weapon.GetFirePoint();

            return SpawnBulletUseCase.SpawnBullet(weapon, gameContext, firePoint );
        }
    }
}