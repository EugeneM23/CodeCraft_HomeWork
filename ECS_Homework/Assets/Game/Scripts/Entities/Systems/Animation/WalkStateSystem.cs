using Game.Scripts.UI.Entities.Components.AttackDistance;
using Game.Scripts.UI.Entities.Components.Health;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public partial struct WalkStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (distanceToTarget, attackDistance, animatorEntityRef, entity) in SystemAPI
                     .Query<DistanceToTarget, AttackDistance, AnimatorEntityRefComponent>()
                     .WithEntityAccess()
                     .WithAny<IsWalking>())
        {
            Debug.Log("Walk state");

            if (distanceToTarget.Value < attackDistance.Value)
            {
                ecb.AddComponent<IsAttaking>(entity);
                ecb.RemoveComponent<IsWalking>(entity);

                animatorEntityRef.SetAnimationState(ref state, ("IsAttacking", true), ("IsWalking", false));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

public partial struct AttackStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (distanceToTarget, attackDistance, animatorEntityRef, entity) in SystemAPI
                     .Query<DistanceToTarget, AttackDistance, AnimatorEntityRefComponent>()
                     .WithEntityAccess()
                     .WithAny<IsAttaking>())
        {
            if (distanceToTarget.Value > attackDistance.Value)
            {
                ecb.AddComponent<IsWalking>(entity);
                ecb.RemoveComponent<IsAttaking>(entity);

                animatorEntityRef.SetAnimationState(ref state, ("IsWalking", true), ("IsAttacking", false));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

public partial struct DeathStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (animatorEntityRef, health) in SystemAPI
                     .Query<AnimatorEntityRefComponent, RefRO<Health>>())
        {
            if (health.ValueRO.Value < 0) 
                animatorEntityRef.SetAnimationState(ref state, ("IsDead", true));
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

public static class AnimatorEntityRefExtensions
{
    public static void SetAnimationState(this in AnimatorEntityRefComponent animatorRef, ref SystemState state,
        params (string name, bool value)[] parameters)
    {
        var em = state.EntityManager;
        var animatorParams = em.GetBuffer<AnimatorControllerParameterComponent>(animatorRef.animatorEntity);
        var indexTable =
            em.GetComponentData<AnimatorControllerParameterIndexTableComponent>(animatorRef.animatorEntity);

        var paramAspect = new AnimatorParametersAspect(animatorParams, indexTable);

        foreach (var (name, value) in parameters)
        {
            paramAspect.SetParameterValue(name, value);
        }
    }
}

public struct SpawnProjectileTag : IComponentData
{
}