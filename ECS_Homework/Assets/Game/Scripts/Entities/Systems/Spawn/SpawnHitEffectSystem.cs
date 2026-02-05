using Game.Scripts.UI.Entities.Components.Health;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateAfter(typeof(ApplyDamageSystem))]
public partial struct SpawnHitEffectSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var manager = state.EntityManager;

        // Обрабатываем все запросы на спавн эффекта
        foreach (var (request, entity) in SystemAPI.Query<HitEffectRequest>()
                     .WithEntityAccess())
        {

            // Получаем позицию цели
            var targetTransform = manager.GetComponentData<LocalTransform>(request.Target);

            // Получаем префаб эффекта
            var hitEffect = manager.GetComponentData<HitEffect>(request.Target);

            // Создаём запрос на спавн эффекта
            Entity spawnRequest = ecb.CreateEntity();
            ecb.AddComponent(spawnRequest, new SpawnPrefabRequest
            {
                Position = targetTransform.Position,
                Prefab = hitEffect.Prefab
            });

            // Удаляем запрос
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(manager);
        ecb.Dispose();
    }
}