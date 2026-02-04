using Unity.Entities;
using UnityEngine;

public class SpeedAuthoring : MonoBehaviour
{
    public float Speed;

    private class SpeddBaker : Baker<SpeedAuthoring>
    {
        public override void Bake(SpeedAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Speed { Value = authoring.Speed });
        }
    }
}