using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Scripts.Entities.Systems
{
    public static class TargetRaycastUseCase
    {
        public static Entity FindClosestByTag<TTag>(CollisionWorld collisionWorld, ComponentLookup<TTag> tagLookup, float3 position, float range)
            where TTag : unmanaged, IComponentData
        {
            var hits = new NativeList<DistanceHit>(Allocator.Temp);

            var filter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = ~0u,
                GroupIndex = 0
            };

            collisionWorld.OverlapSphere(position, range, ref hits, filter);

            Entity closestTarget = Entity.Null;
            float closestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                Entity hitEntity = collisionWorld.Bodies[hit.RigidBodyIndex].Entity;

                if (tagLookup.HasComponent(hitEntity) && hit.Distance < closestDistance)
                {
                    closestDistance = hit.Distance;
                    closestTarget = hitEntity;
                }
            }

            hits.Dispose();
            return closestTarget;
        }

        public static bool IsTargetValid<TTag>(
            Entity targetEntity,
            ComponentLookup<TTag> tagLookup,
            ComponentLookup<LocalTransform> transformLookup,
            float3 currentPosition,
            float range)
            where TTag : unmanaged, IComponentData
        {
            if (targetEntity == Entity.Null)
                return false;

            if (!tagLookup.HasComponent(targetEntity))
                return false;

            if (!transformLookup.TryGetComponent(targetEntity, out var targetTransform))
                return false;

            float distanceSq = math.distancesq(currentPosition, targetTransform.Position);
            return distanceSq <= range * range;
        }
    }
}