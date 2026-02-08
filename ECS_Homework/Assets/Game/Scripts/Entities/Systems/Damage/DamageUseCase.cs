using Game.Scripts.UI.Entities.Components.Health;
using Unity.Entities;

public static class DamageUseCase
{
    public static void DealDamage(SystemState state, DamageRequest damageRequest)
    {
        Health health = state.EntityManager.GetComponentData<Health>(damageRequest.Target);
        health.Value -= damageRequest.DamageAmount;
        state.EntityManager.SetComponentData(damageRequest.Target, health);
    }
}