using Game.Scripts.UI.Entities.Components.Health;
using Rukhanka;
using Unity.Entities;

public partial struct DeathStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (animatorRef, health) in SystemAPI.Query<AnimatorEntityRefComponent, RefRO<Health>>())
        {
            if (health.ValueRO.Value < 0)
            {
                animatorRef.SetAnimationState(ref state, ("IsDead", true));
            }
        }
    }
}