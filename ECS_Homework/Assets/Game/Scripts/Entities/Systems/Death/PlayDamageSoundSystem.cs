using AudioEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateAfter(typeof(ApplyDamageSystem))]
public partial struct PlayDamageSoundSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var manager = state.EntityManager;

        // Воспроизводим звук попадания
        foreach (var (request, entity) in SystemAPI.Query<DamageSoundRequest>()
                     .WithEntityAccess())
        {
            if (request.Target != Entity.Null &&
                manager.Exists(request.Target) &&
                manager.HasComponent<LocalTransform>(request.Target))
            {
                var transform = manager.GetComponentData<LocalTransform>(request.Target);
                AudioSystem.Instance.PlayEvent(MasterBankAPI.FootmanHitEvent, transform.Position);
            }

            ecb.DestroyEntity(entity);
        }

        // Воспроизводим звук смерти
        foreach (var (deathEvent, transform, entity) in SystemAPI.Query<DeathEvent, RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            AudioSystem.Instance.PlayEvent(MasterBankAPI.DeathEvent, transform.ValueRO.Position);
        }

        ecb.Playback(manager);
        ecb.Dispose();
    }
}