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

        /// <summary>
        /// Shakes the scene player's camera, if there is one. A shake is only a look: with no game or
        /// player context (a test scene, or a context whose PlayerContext did not install) the blast,
        /// the wreck or the air bomb goes on without it.
        /// </summary>
        public static void ShakePlayerCamera(CameraShakeArgs args)
        {
            if (GameContext.TryGetInstance(out GameContext game) && game.TryGetPlayerContext(out PlayerContext player))
                ShakePlayerCamera(player, args);
        }
    }
}
