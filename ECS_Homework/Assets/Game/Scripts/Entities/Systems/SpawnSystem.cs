using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct SpawnPrefabSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, entity) in SystemAPI.Query<SpawnPrefabRequest>().WithEntityAccess())
        {
            var ins = ecb.Instantiate(request.UnitPrefab);

            ecb.SetComponent(ins, new LocalTransform
            {
                Position = request.Position,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}