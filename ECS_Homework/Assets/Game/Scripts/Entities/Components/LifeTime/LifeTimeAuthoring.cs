using Unity.Entities;
using UnityEngine;

public class LifeTimeAuthoring : MonoBehaviour
{
    public float LifeTime;

    private class LifeTimeBaker : Baker<LifeTimeAuthoring>
    {
        public override void Bake(LifeTimeAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new LifeTime { Value = authoring.LifeTime, TimeLeft = authoring.LifeTime });
        }
    }
}