using Unity.Entities;
using UnityEngine;

public class EntityRadiusAuthoring : MonoBehaviour
{
    public float Radius = 1;

    private class EntityRadiusBaker : Baker<EntityRadiusAuthoring>
    {
        public override void Bake(EntityRadiusAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EntityRadius { Value = authoring.Radius });
        }
    }
}