using Unity.Entities;
using UnityEngine;

public class WalkStateTagAuthoring : MonoBehaviour
{
    private class WalkStateTagBaker : Baker<WalkStateTagAuthoring>
    {
        public override void Bake(WalkStateTagAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new IsWalking());
        }
    }
}