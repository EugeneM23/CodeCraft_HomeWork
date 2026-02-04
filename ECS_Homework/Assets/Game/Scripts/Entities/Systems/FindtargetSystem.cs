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

            // Для игроков ищем ближайшего врага
            foreach (var (target,cooldown, transform) in SystemAPI.Query<RefRW<Target>,RefRW<UpdateTargetCooldown>, RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                // Обновляем таймер
                cooldown.ValueRW.UpdateTimer -= deltaTime;

                // Проверяем, нужно ли обновлять цель
                if (cooldown.ValueRO.UpdateTimer <= 0f)
                {
                    // Сбрасываем таймер
                    cooldown.ValueRW.UpdateTimer = cooldown.ValueRO.UpdateInterval;

                    // Попытка найти ближайшего врага в радиусе
                    float searchRange = cooldown.ValueRO.Range; // Задайте нужный радиус поиска
                    Entity closestEnemy = TargetRaycastUseCase.FindClosestByTag(
                        collisionWorld,
                        enemyTagLookup,
                        transform.ValueRO.Position,
                        searchRange
                    );

                    // Если нашли врага - используем его, иначе - замок врага
                    target.ValueRW.Value = closestEnemy != Entity.Null ? closestEnemy : enemyCastel;
                }
            }

            // Для врагов ищем ближайшего игрока
            foreach (var (target, cooldown, transform) in SystemAPI.Query<RefRW<Target>, RefRW<UpdateTargetCooldown>, RefRO<LocalTransform>>().WithAll<EnemyTag>())
            {
                // Обновляем таймер
                cooldown.ValueRW.UpdateTimer -= deltaTime;

                // Проверяем, нужно ли обновлять цель
                if (cooldown.ValueRO.UpdateTimer <= 0f)
                {
                    // Сбрасываем таймер
                    cooldown.ValueRW.UpdateTimer = cooldown.ValueRO.UpdateInterval;

                    // Попытка найти ближайшего игрока в радиусе
                    float searchRange = cooldown.ValueRO.Range; // Задайте нужный радиус поиска
                    Entity closestPlayer = TargetRaycastUseCase.FindClosestByTag(
                        collisionWorld,
                        playerTagLookup,
                        transform.ValueRO.Position,
                        searchRange
                    );

                    // Если нашли игрока - используем его, иначе - замок игрока
                    target.ValueRW.Value = closestPlayer != Entity.Null ? closestPlayer : playerCastel;
                }
            }
        }
    }
}