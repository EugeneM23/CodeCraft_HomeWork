using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game
{
    public static class HealthUseCase
    {
        public static bool IsAlive(this IEntity entity)
        {
            int health = entity.GetHealth().Value;

            Debug.Log(health);
            return health > 0;
        }

        public static void TakeDamage(this IEntity entity, int damage)
        {
            entity.GetHealth().Value -= damage;
        }
    }
}