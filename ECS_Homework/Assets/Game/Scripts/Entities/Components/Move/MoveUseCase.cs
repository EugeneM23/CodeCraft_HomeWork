using Unity.Mathematics;
using Unity.Transforms;

public static class MoveUseCase
{
    private const float MinDirectionSqr = 0.001f;

    public static void Move(ref LocalTransform transform, float3 targetPosition, float speed, float deltaTime)
    {
        float3 direction = targetPosition - transform.Position;
        direction.y = 0f;

        if (math.lengthsq(direction) < MinDirectionSqr)
            return;

        direction = math.normalize(direction);
        transform.Position += direction * speed * deltaTime;
    }
}