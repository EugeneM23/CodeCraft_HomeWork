using Game.Scripts.UI.Entities.Components.Health;
using Unity.Collections;
using Unity.Entities;

public partial struct ApplyDamageSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (damageRequest, entity) in SystemAPI.Query<DamageRequest>().WithEntityAccess())
        {
            RefRW<Health> targetHealth = SystemAPI.GetComponentRW<Health>(damageRequest.Target);
            targetHealth.ValueRW.Value -= damageRequest.DamageAmount;

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}