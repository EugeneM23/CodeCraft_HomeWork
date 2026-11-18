using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CameraFollowController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private IReactiveVariable<IEntity> _characterSource;
        private Subscription<IEntity> _characterSubscription;

        private Transform _cameraPoint;
        private float _distance = 1f;
        private IValue<Vector3> _offset;
        private Transform _cameraRoot;
        private IValue<int> _speed;
        private IVariable<Vector3> _look;
        private CameraLookSettings _lookSettings;

        private CameraCollisionSettings _collision;
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private Transform _ignore;
        // The camera under the root: tilted by the same swing as the root, so the target stays put
        // on screen. Null when the camera is the root itself (then it is not tilted).
        private Transform _camera;
        // The camera's own turn under the root, as set in the scene: the swing goes on top of it.
        private Quaternion _cameraRest = Quaternion.identity;
        // Where the follow would put the camera with no houses about; the camera rises from here.
        private Vector3 _followPosition;
        private bool _hasFollowPosition;
        // Metres the camera is raised over a house, eased.
        private float _rise;

        // A snap asked for by RequestSnap: the camera jumps to the target instead of easing there
        private static int s_snapRequests;
        private int _snapsSeen;

        /// <summary>
        /// Every follow camera jumps to its target on its next LateTick instead of easing there: the tank was moved
        /// far in one go (the run rebuilds the arena and places the tank under the closed shutter). A counter, not a
        /// search: costs one compare a frame.
        /// </summary>
        public static void RequestSnap() => s_snapRequests++;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_snapRequests = 0;

        public void Init(IPlayerContext context)
        {
            _offset = context.GetCameraOffset();
            _cameraRoot = context.GetCameraRoot();
            _speed = context.GetCameraSpeed();

            // Optional: without a look the camera follows exactly as it did before.
            context.TryGetCameraLook(out _look);
            context.TryGetCameraLookSettings(out _lookSettings);
            // Optional too: a context without it (the tests) follows through houses.
            context.TryGetCameraCollisionSettings(out _collision);

            if (context.TryGetCamera(out IEntity camera) && camera != null
                && camera.TryGetTransform(out Transform cameraTransform)
                && cameraTransform != _cameraRoot && cameraTransform.IsChildOf(_cameraRoot))
            {
                _camera = cameraTransform;
                _cameraRest = cameraTransform.localRotation;
            }

            _snapsSeen = s_snapRequests;
            _characterSource = context.GetCharacter();
            _characterSubscription = _characterSource.Observe(OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context) => _characterSubscription.Dispose();

        /// <summary>
        /// Something bigger than a person — a tank — asks for the camera further out with
        /// CameraDistance. The whole offset scales, so the view keeps its angle and only widens.
        /// No easing is needed for the switch: the follow below already eases the root towards
        /// wherever the offset now puts it.
        /// </summary>
        private void OnCharacterChanged(IEntity character)
        {
            _cameraPoint = character?.GetCameraPoint();
            _distance = character != null && character.TryGetCameraDistance(out float distance) ? distance : 1f;
            // The camera must not stop at the colliders of what it follows (the tank's hull).
            _ignore = character != null && character.TryGetTransform(out Transform body) ? body : null;
        }

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            if (_cameraPoint == null) return;

            // The look leans the camera towards the cursor. It goes into the target rather than onto
            // the root, so the same easing that follows the character also smooths a flick of the mouse.
            Vector3 offset = _look != null && _lookSettings != null
                ? CameraLookUseCase.Offset(_offset.Value, _distance, _look.Value, _lookSettings)
                : _offset.Value * _distance;

            if (_snapsSeen != s_snapRequests)
            {
                _snapsSeen = s_snapRequests;
                Snap(offset);
                return;
            }

            if (_collision == null || !_collision.Enabled)
            {
                CameraFollowUseCase.Follow(_cameraRoot, _cameraPoint, deltaTime, _speed.Value, offset);
                _hasFollowPosition = false;
                Tilt(Quaternion.identity);
                return;
            }

            // The follow eases a position of its own, as it eased the root before; the root is then
            // raised over any house from there. Easing the root itself would make the next frame
            // start from the raised position.
            if (!_hasFollowPosition)
            {
                _followPosition = _cameraRoot.position;
                _rise = 0f;
                _hasFollowPosition = true;
            }

            float t = 1f - Mathf.Exp(-_speed.Value * deltaTime);
            _followPosition = Vector3.Lerp(_followPosition, _cameraPoint.position + offset, t);

            Vector3 pivot = _cameraPoint.position;
            Vector3 toCamera = _followPosition - pivot;
            float wanted = toCamera.magnitude;
            if (wanted < 1e-4f)
            {
                _cameraRoot.position = _followPosition;
                Tilt(Quaternion.identity);
                return;
            }

            Vector3 direction = toCamera / wanted;
            int mask = _collision.Obstacles;

            // A house below the camera, in the way: the camera rises straight up, as little as lets
            // it see the tank over the roof. Only what is below it moves it, and only up: a roof or
            // an overhang over the camera never pushes it down or in.
            Vector3 target = pivot + Vector3.up * _collision.TargetHeight;
            float rise = CameraCollisionUseCase.RiseToClear(
                _followPosition, target, _collision.ViewRadius, mask, _ignore, _hits,
                CameraCollisionUseCase.IsBuilding, _collision.MaxRise, _collision.SearchStep, out bool cleared);

            // Nothing clears it (the tank is inside a house, under its roof): no rise either. The
            // camera stays where the follow puts it.
            if (!cleared)
                rise = 0f;

            _rise = CameraCollisionUseCase.Ease(_rise, rise, _collision.RiseSpeed, _collision.ReturnSpeed, deltaTime);
            Vector3 raised = _followPosition + Vector3.up * _rise - pivot;
            Vector3 raisedDirection = raised.normalized;

            _cameraRoot.position = pivot + raised;

            // Tilt the view down by as much as the camera rose, so the tank stays where it was on screen.
            Tilt(Quaternion.FromToRotation(direction, raisedDirection));
        }

        // Straight onto the target, nothing eased and no rise kept: the next frames follow from here
        private void Snap(Vector3 offset)
        {
            Vector3 position = _cameraPoint.position + offset;
            _cameraRoot.position = position;
            _followPosition = position;
            _hasFollowPosition = _collision != null && _collision.Enabled;
            _rise = 0f;
            Tilt(Quaternion.identity);
        }

        // Applies a world-space swing to the camera under the root, on top of the camera's own turn
        // from the scene, leaving the root's own rotation (the game's angle, the sandbox orbit) alone.
        // Writing the swing alone threw that turn away: a camera tilted in the scene looked
        // straight along the root in Play.
        private void Tilt(Quaternion swing)
        {
            if (_camera == null)
                return;

            Quaternion rig = _cameraRoot.rotation;
            _camera.localRotation = Quaternion.Inverse(rig) * swing * rig * _cameraRest;
        }
    }
}
