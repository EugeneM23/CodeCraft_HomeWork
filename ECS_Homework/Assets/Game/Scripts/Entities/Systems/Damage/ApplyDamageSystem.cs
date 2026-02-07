using Game.Scripts.UI.Entities.Components.Health;
using Unity.Collections;
using Unity.Entities;

public partial struct ApplyDamageSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (damageRequest, health, entity) in SystemAPI
                     .Query<DamageRequest, RefRW<Health>>()
                     .WithEntityAccess())
        {
            health.ValueRW.Value -= damageRequest.DamageAmount;
            ecb.RemoveComponent<DamageRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}