using Game.Animation;
using Game.Scripts.Entities.Systems;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct WalkStateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (target, entity) in SystemAPI.Query<Target>().WithEntityAccess().WithAny<IsWalking>())
        {
            Debug.Log("Walk state");
            var entityTransform = state.EntityManager.GetComponentData<LocalTransform>(entity);
            var targetTransfrom = state.EntityManager.GetComponentData<LocalTransform>(target.Value);
            var distance = math.distance(entityTransform.Position, targetTransfrom.Position);

            if (distance < 3)
            {
                ecb.AddComponent<IsAttaking>(entity);
                ecb.RemoveComponent<IsWalking>(entity);
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

        foreach (var (target, entity) in SystemAPI.Query<Target>().WithEntityAccess().WithAny<IsAttaking>())
        {
            Debug.Log("attack state");
            var entityTransform = state.EntityManager.GetComponentData<LocalTransform>(entity);
            var targetTransfrom = state.EntityManager.GetComponentData<LocalTransform>(target.Value);
            var distance = math.distance(entityTransform.Position, targetTransfrom.Position);

            if (distance > 3)
            {
                ecb.AddComponent<IsWalking>(entity);
                ecb.RemoveComponent<IsAttaking>(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}



public partial struct AnimationStateControllerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Переключение на атаку
        foreach (var animatorEntityRef in SystemAPI.Query<AnimatorEntityRefComponent>()
                     .WithAll<IsAttaking>()
                     .WithNone<IsWalking>())
        {
            Debug.Log("asdasda");
            var animatorEntity = animatorEntityRef.animatorEntity;
            
            var animatorParams = SystemAPI.GetBuffer<AnimatorControllerParameterComponent>(animatorEntity);
            var indexTable = SystemAPI.GetComponent<AnimatorControllerParameterIndexTableComponent>(animatorEntity);
            
            var paramAspect = new AnimatorParametersAspect(animatorParams, indexTable);
            paramAspect.SetParameterValue("IsAttacking", true);
            paramAspect.SetParameterValue("IsWalking", false);
        }

        // Переключение на ходьбы
        foreach (var animatorEntityRef in SystemAPI.Query<AnimatorEntityRefComponent>()
                     .WithAll<IsWalking>()
                     .WithNone<IsAttaking>())
        {
            var animatorEntity = animatorEntityRef.animatorEntity;
            
            var animatorParams = SystemAPI.GetBuffer<AnimatorControllerParameterComponent>(animatorEntity);
            var indexTable = SystemAPI.GetComponent<AnimatorControllerParameterIndexTableComponent>(animatorEntity);
            
            var paramAspect = new AnimatorParametersAspect(animatorParams, indexTable);
            paramAspect.SetParameterValue("IsWalking", true);
            paramAspect.SetParameterValue("IsAttacking", false);
        }
    }
}