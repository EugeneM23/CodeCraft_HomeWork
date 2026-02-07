using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.Entities.Systems
{
    public class TargetUpdateAuthoring : MonoBehaviour
    {
        public float UpdateInterval;
        public float Range = 3f;

        private class UpdateTargetCooldownBaker : Baker<TargetUpdateAuthoring>
        {
            public override void Bake(TargetUpdateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new TargetUpdate
                {
                    UpdateInterval = authoring.UpdateInterval,
                    Range = authoring.Range
                });
            }
        }
    }
}