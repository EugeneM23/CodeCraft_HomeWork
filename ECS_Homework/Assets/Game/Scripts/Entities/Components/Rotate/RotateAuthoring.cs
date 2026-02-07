using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Scripts.Components
{
    public class RotateAuthoring : MonoBehaviour
    {
        public float RotationSpeed;

        public class Baker : Baker<RotateAuthoring>
        {
            public override void Bake(RotateAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Rotate
                {
                    RotationSpeed = authoring.RotationSpeed,
                    TargetDirection = new float3(0, 0, 1)
                });
            }
        }
    }
}

