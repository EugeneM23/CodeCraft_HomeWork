using Unity.Entities;
using Unity.Mathematics;

namespace Game.Scripts.Components
{
    public struct Rotate : IComponentData
    {
        public float RotationSpeed;
        public float3 TargetDirection;
    }
}