using UnityEngine;

namespace Game
{
    public static class CameraFollowUseCase
    {
        public static void Follow(
            in Transform source,
            in Transform target,
            in float deltaTime,
            in int speed = 5,
            Vector3 offset = default
        )
        {
            // Frame-rate independent smoothing. `speed * deltaTime` made the follow lag depend on
            // frame rate (0.083 at 60 fps vs 0.035 at 144) and could exceed 1.0 on a long frame.
            float t = 1f - Mathf.Exp(-speed * deltaTime);
            source.position = Vector3.Lerp(source.position, target.position + offset, t);
        }
    }
}