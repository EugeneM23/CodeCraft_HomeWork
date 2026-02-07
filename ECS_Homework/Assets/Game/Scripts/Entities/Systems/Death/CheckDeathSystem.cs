using Game.Scripts.UI.Entities.Components.Health;
using Unity.Collections;
using Unity.Entities;

[UpdateAfter(typeof(ApplyDamageSystem))]
public partial struct CheckDeathSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (health, entity) in SystemAPI.Query<RefRO<Health>>().WithEntityAccess())
            if (health.ValueRO.Value <= 0)
                ecb.AddComponent<DeathEvent>(entity);

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}