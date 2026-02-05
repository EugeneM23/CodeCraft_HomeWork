using Game.Scripts.Entities.Systems;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Game.Scripts.Targeting
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct FindTargetSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var teamMaskLookup = SystemAPI.GetComponentLookup<TeamMask>(true);
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (target, cooldown, transform, myTeam) in SystemAPI
                         .Query<RefRW<Target>, RefRW<TargetUpdateSettings>, RefRO<LocalTransform>, RefRO<TeamMask>>())
            {
                cooldown.ValueRW.UpdateTimer -= deltaTime;

                if (cooldown.ValueRO.UpdateTimer > 0f) continue;

                cooldown.ValueRW.UpdateTimer = cooldown.ValueRO.UpdateInterval;

                target.ValueRW.Value =
                    FindTargetUseCase.FindClosestEnemy(collisionWorld, teamMaskLookup, transform, cooldown, myTeam);

                if (target.ValueRW.Value == Entity.Null)
                    target.ValueRW.Value = FindTargetUseCase.FindEnemyCastle(state.EntityManager, myTeam);
            }
        }
    }
}