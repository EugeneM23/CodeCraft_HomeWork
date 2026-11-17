using Atomic.Elements;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Reads where the cursor sits on the screen and writes CameraLook — the way the camera should
    /// lean, on the ground, at a strength from 0 to 1 (more behind the character, where the tilted
    /// camera shows less ground). CameraFollowController turns it into a shift and a pull-back.
    ///
    /// Runs in Tick so the look is ready before the follow in LateTick reads it. Nothing here knows
    /// whether the player is on foot or in a tank: the follow scales the lean by CameraDistance.
    /// </summary>
    public class CameraLookController : IPlayerContextInit, IPlayerContextTick
    {
        private IVariable<Vector3> _look;
        private CameraLookSettings _settings;
        private UnityEngine.Camera _camera;

        public void Init(IPlayerContext context)
        {
            _look = context.GetCameraLook();
            _settings = context.GetCameraLookSettings();
            _camera = UnityEngine.Camera.main;
        }

        public void Tick(IPlayerContext context, float deltaTime)
        {
            if (_camera == null)
            {
                // The camera can be destroyed on scene teardown; re-resolving is cheap enough
                // here because it only happens once the cached one is gone.
                _camera = UnityEngine.Camera.main;
                if (_camera == null) return;
            }

            Vector2 screenLook = CameraLookUseCase.FromCursor(
                CursorUseCase.ScreenPoint,
                new Vector2(UnityEngine.Screen.width, UnityEngine.Screen.height),
                _settings.DeadZone);

            Transform cameraTransform = _camera.transform;

            // Read off the live camera, so a change of angle or field of view re-balances by itself.
            float backwardBoost = _camera.orthographic
                ? 1f
                : CameraLookUseCase.BackwardBoost(cameraTransform.forward, _camera.fieldOfView);

            screenLook = CameraLookUseCase.CompensateTilt(screenLook, backwardBoost);
            _look.Value = CameraLookUseCase.ToGround(screenLook, cameraTransform.right, cameraTransform.up);
        }
    }
}
