using UnityEngine;

namespace Game
{
    public static class ShakeTransformUseCase
    {
        public static void ShakePosition(Transform transform, Vector3 originalPosition, 
            float strength, float normalizedTime)
        {
            float dampingFactor = normalizedTime; 

            float x = Random.Range(-1f, 1f) * strength * dampingFactor;
            float y = Random.Range(-1f, 1f) * strength * dampingFactor;

            transform.localPosition = originalPosition + new Vector3(x, y, 0f);
        }
    }
}