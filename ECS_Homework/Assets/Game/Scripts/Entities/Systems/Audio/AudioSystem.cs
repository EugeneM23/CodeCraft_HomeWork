using Unity.Collections;
using Unity.Entities;
using UnityEngine;

//[UpdateBefore(typeof(ApplyDamageSystem))]
public partial struct AudioSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, entity) in SystemAPI.Query<AudioRequest>().WithEntityAccess())
        {
            AudioEngine.AudioSystem.Instance.PlayEvent(request.SoundName.Value, request.Position, 0.05f);
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}