using Game.Scripts.Entities.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Scripts.Targeting
{
    public static class FindTargetUseCase
    {
        public static Entity FindClosestEnemy(
            CollisionWorld collisionWorld,
            ComponentLookup<TeamMask> teamMaskLookup,
            RefRO<LocalTransform> transform,
            RefRW<TargetUpdateSettings> settings,
            RefRO<TeamMask> myTeam)
        {
            Entity closestEnemy = Entity.Null;
            float closestDistance = settings.ValueRO.Range;

            var hits = new NativeList<DistanceHit>(Allocator.Temp);

            if (collisionWorld.OverlapSphere(transform.ValueRO.Position, settings.ValueRO.Range, ref hits,
                new CollisionFilter { BelongsTo = ~0u, CollidesWith = ~0u }))
            {
                foreach (var hit in hits)
                {
                    if (!teamMaskLookup.HasComponent(hit.Entity))
                        continue;

                    TeamMask targetTeam = teamMaskLookup[hit.Entity];

                    if (TeamRelations.IsEnemy(myTeam.ValueRO.Team, targetTeam.Team))
                    {
                        float distance = hit.Distance;
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestEnemy = hit.Entity;
                        }
                    }
                }
            }

            hits.Dispose();
            return closestEnemy;
        }

        public static Entity FindEnemyCastle(EntityManager entityManager, RefRO<TeamMask> myTeam)
        {
            CastleTargetType castleType = TeamRelations.GetEnemyCastleType(myTeam.ValueRO.Team);

            switch (castleType)
            {
                case CastleTargetType.EnemyCastle:
                    var enemyQuery = entityManager.CreateEntityQuery(typeof(EnemyCastelTag));
                    if (!enemyQuery.IsEmpty)
                        return enemyQuery.GetSingletonEntity();
                    break;

                case CastleTargetType.PlayerCastle:
                    var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerCastleTag));
                    if (!playerQuery.IsEmpty)
                        return playerQuery.GetSingletonEntity();
                    break;
            }

            return Entity.Null;
        }
    }
}