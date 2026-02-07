using AudioEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateBefore(typeof(ApplyDamageSystem))]
public partial struct PlayDamageSoundSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, entity) in SystemAPI
                     .Query<DamageSoundRequest>()
                     .WithEntityAccess())
        {
            var localTransform = state.EntityManager.GetComponentData<LocalTransform>(request.Target);
            AudioSystem.Instance.PlayEvent(request.SoundName.Value, localTransform.Position, 0.1f);
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}