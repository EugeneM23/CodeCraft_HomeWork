using UnityEngine;

namespace Game
{
    public static class ShakeTransformUseCase
    {
        public static Vector3 CalculateShakePosition(Vector3 originalPosition, float strength, float normalizedTime)
        {
            float x = Random.Range(-1f, 1f) * strength;
            float y = Random.Range(-1f, 1f) * strength;

            return originalPosition + new Vector3(x, y, 0f);
        }

        public static Vector3 CalculateShakePositionWithDamping(Vector3 originalPosition, float strength,
            float normalizedTime)
        {
            float dampingFactor = normalizedTime; // Затухание от 1 до 0

            float x = Random.Range(-1f, 1f) * strength * dampingFactor;
            float y = Random.Range(-1f, 1f) * strength * dampingFactor;

            return originalPosition + new Vector3(x, y, 0f);
        }
    }
}