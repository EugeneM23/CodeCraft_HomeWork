using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game.Scripts.Gameplay
{
    public static class HealthUseCase
    {
        public static bool IsAlive(this IEntity entity)
        {
            int health = entity.GetHealth().Value;

            Debug.Log(health);
            return health > 0;
        }
    }
}