using Game.Scripts.UI.Entities.Components.Health;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Система отвечает ТОЛЬКО за применение урона к цели
/// </summary>
public partial struct ApplyDamageSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Обрабатываем все запросы на нанесение урона
        foreach (var (damageRequest, entity) in SystemAPI.Query<DamageRequest>()
                     .WithEntityAccess())
        {
            // Проверяем, что цель существует и имеет здоровье
            if (damageRequest.Target == Entity.Null ||
                !SystemAPI.Exists(damageRequest.Target) ||
                !SystemAPI.HasComponent<Health>(damageRequest.Target))
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            // Применяем урон
            RefRW<Health> targetHealth = SystemAPI.GetComponentRW<Health>(damageRequest.Target);
            targetHealth.ValueRW.Value -= damageRequest.DamageAmount;

            // Удаляем запрос
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}