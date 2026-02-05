using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.Entities.Systems
{
    public class UpdateTargetCooldownAuthoring : MonoBehaviour
    {
        public float UpdateInterval;
        public float Range = 3f;

        private class UpdateTargetCooldownBaker : Baker<UpdateTargetCooldownAuthoring>
        {
            public override void Bake(UpdateTargetCooldownAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new TargetUpdateSettings
                {
                    UpdateInterval = authoring.UpdateInterval,
                    Range = authoring.Range
                });
            }
        }
    }
}