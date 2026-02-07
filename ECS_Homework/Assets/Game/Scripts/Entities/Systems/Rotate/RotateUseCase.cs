using Unity.Mathematics;
using Unity.Transforms;

public static class RotateUseCase
{
    private const float MinDirectionSqr = 0.001f;

    public static void RotateTowards(ref LocalTransform transform, float3 targetPosition,
        float rotationSpeedDegrees, float deltaTime)
    {
        float3 direction = targetPosition - transform.Position;
        direction.y = 0f;

        if (math.lengthsq(direction) < MinDirectionSqr)
            return;

        direction = math.normalize(direction);
        quaternion targetRotation = quaternion.LookRotationSafe(direction, math.up());
        
        float angle = GetAngleBetweenRotations(transform.Rotation, targetRotation);
        float maxDeltaAngle = rotationSpeedDegrees * deltaTime;
        float t = angle > 0.001f ? math.saturate(maxDeltaAngle / angle) : 1f;

        transform.Rotation = math.slerp(transform.Rotation, targetRotation, t);
    }

    private static float GetAngleBetweenRotations(quaternion from, quaternion to)
    {
        float dot = math.abs(math.dot(from, to));
        float angleRadians = 2.0f * math.acos(math.min(dot, 1.0f));
        return math.degrees(angleRadians);
    }
}