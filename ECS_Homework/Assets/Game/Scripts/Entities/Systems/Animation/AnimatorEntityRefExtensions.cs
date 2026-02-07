using Rukhanka;
using Unity.Entities;

public static class AnimatorEntityRefExtensions
{
    public static void SetAnimationState(this in AnimatorEntityRefComponent animatorRef, ref SystemState state,
        params (string name, bool value)[] parameters)
    {
        var animatorParams = state.EntityManager.GetBuffer<AnimatorControllerParameterComponent>(animatorRef.animatorEntity);
        var indexTable = state.EntityManager.GetComponentData<AnimatorControllerParameterIndexTableComponent>(animatorRef.animatorEntity);
        var paramAspect = new AnimatorParametersAspect(animatorParams, indexTable);

        foreach (var (name, value) in parameters)
        {
            paramAspect.SetParameterValue(name, value);
        }
    }
}