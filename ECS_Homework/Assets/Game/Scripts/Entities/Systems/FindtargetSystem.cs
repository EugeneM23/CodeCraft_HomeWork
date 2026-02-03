using Unity.Burst;
using Unity.Entities;

namespace Game.Scripts.Entities.Systems
{
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

            foreach (var target in SystemAPI.Query<RefRW<Target>>().WithAny<PlayerTag>())
            {
                if (target.ValueRO.Value == Entity.Null)
                    target.ValueRW.Value = enemyCastel;
            }

            foreach (var target in SystemAPI.Query<RefRW<Target>>().WithAny<EnemyTag>())
            {
                if (target.ValueRO.Value == Entity.Null)
                    target.ValueRW.Value = playerCastel;
            }
        }
    }
}