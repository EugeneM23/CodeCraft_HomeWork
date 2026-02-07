using Unity.Entities;
using Unity.Mathematics;

namespace Game.Scripts.Entities.Systems.Request
{
    public static class RequestUseCase
    {
        public static void DealDamage(EntityCommandBuffer ecb, Entity target, int damageAmount)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new DamageRequest
            {
                Target = target,
                DamageAmount = damageAmount
            });
        }

        public static void SpawnPrefab(EntityCommandBuffer ecb, Entity prefab, float3 position, quaternion rotation)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new SpawnPrefabRequest
            {
                Prefab = prefab,
                Position = position,
                Rotaion = rotation
            });
        }

        public static void PlaySound(EntityCommandBuffer ecb, Entity target, string soundName, float3 position)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new AudioRequest
            {
                Target = target,
                SoundName = soundName,
                Position = position
            });
        }
    }
}