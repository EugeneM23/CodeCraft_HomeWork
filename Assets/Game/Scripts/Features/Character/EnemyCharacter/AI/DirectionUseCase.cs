using UnityEngine;

namespace Game
{
    public static class DirectionUseCase
    {
        public static Vector3 Get(Transform source, Transform target) => target.position - source.position;
    }
}