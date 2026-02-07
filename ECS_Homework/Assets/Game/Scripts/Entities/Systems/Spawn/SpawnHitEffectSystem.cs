using Game.Scripts.UI.Entities.Components.Health;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateBefore(typeof(ApplyDamageSystem))]
[UpdateAfter(typeof(HitEventSystem))]
public partial struct SpawnHitEffectSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var hitEffectLookup = SystemAPI.GetComponentLookup<HitEffect>(true);

        foreach (var (transform, entity) in SystemAPI
                     .Query<RefRO<LocalTransform>>()
                     .WithAll<HitEffectRequest>()
                     .WithEntityAccess())
        {
            if (!hitEffectLookup.HasComponent(entity)) continue;

            var hitEffect = hitEffectLookup[entity];
            var hitPrefab = state.EntityManager.CreateEntity();

            ecb.AddComponent(hitPrefab, new SpawnPrefabRequest
            {
                Prefab = hitEffect.Prefab,
                Position = transform.ValueRO.Position
            });

            ecb.RemoveComponent<HitEffectRequest>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}