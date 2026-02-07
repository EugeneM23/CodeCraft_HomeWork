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
            Health health = state.EntityManager.GetComponentData<Health>(damageRequest.Target);
            health.Value -= damageRequest.DamageAmount;
            
            state.EntityManager.SetComponentData(damageRequest.Target, health);
            
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}