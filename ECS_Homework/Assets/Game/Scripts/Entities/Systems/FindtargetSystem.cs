using System;
using Unity.Entities;
using Unity.Transforms;

namespace Game.Scripts.Entities.Systems
{
    public partial struct FindtargetSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            //state.RequireForUpdate<EnemyCastelTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var enemyCastel = state.EntityManager.CreateEntityQuery(typeof(EnemyCastelTag))
                .GetSingletonEntity();

            foreach (var target in SystemAPI.Query<RefRW<Target>>())
            {
                if (target.ValueRO.Value == Entity.Null)
                    target.ValueRW.Value = enemyCastel;
            }
        }
    }
}