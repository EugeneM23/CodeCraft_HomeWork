using Unity.Entities;
using UnityEngine;

namespace Game.Scripts.UI.Entities.Components.AttackDistance
{
    public class AttackDistanceAuthoring : MonoBehaviour
    {
        public float Value;

        private class AttackDistanceBaker : Baker<AttackDistanceAuthoring>
        {
            public override void Bake(AttackDistanceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                //в
                AddComponent(entity, new AttackDistance { Value = authoring.Value });
            }
        }
    }
}