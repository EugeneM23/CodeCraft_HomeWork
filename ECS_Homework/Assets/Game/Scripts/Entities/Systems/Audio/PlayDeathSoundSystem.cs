using AudioEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateAfter(typeof(CheckDeathSystem))]
[UpdateBefore(typeof(HandleDeathSystem))]
public partial struct PlayDeathSoundSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (transform, entity) in SystemAPI
                     .Query<RefRO<LocalTransform>>()
                     .WithAll<DeathEvent>()
                     .WithNone<DeathSoundPlayed>()
                     .WithEntityAccess())
        {
            AudioSystem.Instance.PlayEvent(MasterBankAPI.DeathEvent, transform.ValueRO.Position);
            ecb.AddComponent<DeathSoundPlayed>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}