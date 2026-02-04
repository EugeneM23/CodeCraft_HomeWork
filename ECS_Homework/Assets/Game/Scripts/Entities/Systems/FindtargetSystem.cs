using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Game.Scripts.Entities.Systems
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct FindTargetSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyCastelTag>();
            state.RequireForUpdate<PlayerCastleTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var enemyCastel = state.EntityManager.CreateEntityQuery(typeof(EnemyCastelTag))
                .GetSingletonEntity();

            var playerCastel = state.EntityManager.CreateEntityQuery(typeof(PlayerCastleTag))
                .GetSingletonEntity();

            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var enemyTagLookup = SystemAPI.GetComponentLookup<EnemyTag>(true);
            var playerTagLookup = SystemAPI.GetComponentLookup<PlayerTag>(true);
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (target,cooldown, transform) in SystemAPI.Query<RefRW<Target>,RefRW<UpdateTargetCooldown>, RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                cooldown.ValueRW.UpdateTimer -= deltaTime;

                if (cooldown.ValueRO.UpdateTimer <= 0f)
                {
                    cooldown.ValueRW.UpdateTimer = cooldown.ValueRO.UpdateInterval;

                    float searchRange = cooldown.ValueRO.Range;
                    Entity closestEnemy = TargetRaycastUseCase.FindClosestByTag(
                        collisionWorld,
                        enemyTagLookup,
                        transform.ValueRO.Position,
                        searchRange
                    );

                    target.ValueRW.Value = closestEnemy != Entity.Null ? closestEnemy : enemyCastel;
                }
            }

            foreach (var (target, cooldown, transform) in SystemAPI.Query<RefRW<Target>, RefRW<UpdateTargetCooldown>, RefRO<LocalTransform>>().WithAll<EnemyTag>())
            {
                cooldown.ValueRW.UpdateTimer -= deltaTime;

                if (cooldown.ValueRO.UpdateTimer <= 0f)
                {
                    cooldown.ValueRW.UpdateTimer = cooldown.ValueRO.UpdateInterval;

                    float searchRange = cooldown.ValueRO.Range;
                    Entity closestPlayer = TargetRaycastUseCase.FindClosestByTag(
                        collisionWorld,
                        playerTagLookup,
                        transform.ValueRO.Position,
                        searchRange
                    );

                    target.ValueRW.Value = closestPlayer != Entity.Null ? closestPlayer : playerCastel;
                }
            }
        }
    }
}