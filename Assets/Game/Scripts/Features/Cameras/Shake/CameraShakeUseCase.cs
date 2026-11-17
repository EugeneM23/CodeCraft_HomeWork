using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Shakes the camera the player looks through: for a blast, a flash or a wreck, wherever it is.
    /// </summary>
    public static class CameraShakeUseCase
    {
        public static void ShakePlayerCamera(IPlayerContext player, CameraShakeArgs args)
        {
            if (args == null || player == null)
                return;

            IEntity camera = player.GetCamera();
            if (camera != null && camera.TryGetCameraShakeEvent(out IEvent<CameraShakeArgs> shake))
                shake.Invoke(args);
        }

        public static void ShakePlayerCamera(CameraShakeArgs args) =>
            ShakePlayerCamera(GameContext.Instance.GetPlayerContext(), args);
    }
}
