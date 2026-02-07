using Unity.Collections;
using Unity.Entities;

public partial struct LifeTimeDespawnSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var timeDeltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (lifeTime, entity) in SystemAPI.Query<RefRW<LifeTime>>().WithEntityAccess())
        {
            lifeTime.ValueRW.TimeLeft -= timeDeltaTime;

            if (lifeTime.ValueRW.TimeLeft <= 0f)
                ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}