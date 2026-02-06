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
            AddComponent(entity, new DistanceToTarget(value: 1000f));
        }
    }
}

public struct DistanceToTarget : IComponentData
{
    public float Value;

    public DistanceToTarget(float value)
    {
        Value = value;
    }
}