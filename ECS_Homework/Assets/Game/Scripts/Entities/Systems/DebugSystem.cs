using Unity.Collections;
using Unity.Entities;
using UnityEditor;

public partial struct DebugSystem : ISystem
{
    private float timer;

    public void OnUpdate(ref SystemState state)
    {
        timer -= SystemAPI.Time.DeltaTime;

        if (timer <= 0)
        {
            timer = 1f;

            var manager = state.EntityManager;
            var catalog = manager.CreateEntityQuery(typeof(UnitEntityCatalog)).GetSingleton<UnitEntityCatalog>();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ecb.Instantiate(catalog.KnightRed);

            ecb.Playback(manager);
            ecb.Dispose();
        }
    }
}

public struct UnitEntityCatalog : IComponentData
{
    public Entity KnightRed;
    public Entity NecromancerRed;
    public Entity KnightBlue;
    public Entity NecromancerBlue;
}