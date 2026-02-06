using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class TargetDistanceAuthoring : MonoBehaviour
{
    private class TargetDistanceBaker : Baker<TargetDistanceAuthoring>
    {
        public override void Bake(TargetDistanceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DistanceToTarget());
        }
    }
}

public struct DistanceToTarget : IComponentData
{
    public float Value;
}