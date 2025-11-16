using UnityEngine;

namespace Game
{
    public static class FollowObjectUseCase
    {
        public static void Follow(
            in Transform source,
            in Transform target,
            in float deltaTime,
            in int speed = 5,
            Vector3 offset = default
        )
        {
            source.position = Vector3.Lerp(source.position, target.position + offset, speed * deltaTime);
        }
    }
}