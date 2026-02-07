using AudioEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateBefore(typeof(ApplyDamageSystem))]
public partial struct PlayDamageSoundSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        
        foreach (var (request, transform, entity) in SystemAPI
                     .Query<DamageSoundRequest, RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            AudioSystem.Instance.PlayEvent(MasterBankAPI.FootmanHitEvent, transform.ValueRO.Position);
            ecb.RemoveComponent<DamageSoundRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}