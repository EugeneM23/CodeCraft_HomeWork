using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.Entities.Systems
{
    public class TargetAuthoring : MonoBehaviour
    {
        public float UpdateInterval = 0.2f;

        private class TargetBaker : Baker<TargetAuthoring>
        {
            public override void Bake(TargetAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Target()
                {
                    UpdateInterval = authoring.UpdateInterval
                });
            }
        }
    }
}