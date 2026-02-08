using Game.Scripts.UI.Entities.Components.Health;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

public partial struct ApplyDamageSystem : ISystem
{
    private ComponentLookup<HitEffect> _hitEffectLookUP;
    private ComponentLookup<LocalTransform> _transformLookUp;

    public void OnCreate(ref SystemState state)
    {
        _hitEffectLookUP = SystemAPI.GetComponentLookup<HitEffect>(true);
        _transformLookUp = SystemAPI.GetComponentLookup<LocalTransform>(true);
    }

    public void OnUpdate(ref SystemState state)
    {
        _hitEffectLookUP.Update(ref state);
        _transformLookUp.Update(ref state);

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (damageRequest, entity) in SystemAPI
                     .Query<DamageRequest>().WithEntityAccess().WithNone<IsDead>())
        {
            DamageUseCase.DealDamage(state, damageRequest);

            var hitEffectRequest = ecb.CreateEntity();
            ecb.AddComponent(hitEffectRequest, new SpawnPrefabRequest
            {
                Prefab = _hitEffectLookUP[damageRequest.Target].Prefab,
                Position = _transformLookUp[damageRequest.Target].Position,
                Rotation = _transformLookUp[damageRequest.Target].Rotation
            });

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}