using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Aims the character at the mouse cursor.
    ///
    /// This replaces the on-screen rotation joystick, which was the only source of
    /// RotateDirection — so on a desktop build the value stayed at zero and the character
    /// never turned at all.
    ///
    /// The cursor is projected onto a horizontal plane through the character rather than
    /// raycast against level colliders, deliberately: a good part of this level has floor
    /// without colliders, and aiming must not break over those gaps.
    /// </summary>
    public class CharacterRotateController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private IReactiveVariable<IEntity> _characterSource;
        private Subscription<IEntity> _characterSubscription;

        private IReactiveVariable<Vector3> _rotateDirection;
        private Transform _characterTransform;
        private UnityEngine.Camera _camera;

        public void Init(IPlayerContext context)
        {
            _characterSource = context.GetCharacter();
            _characterSubscription = _characterSource.Observe(OnCharacterChanged);
            _camera = UnityEngine.Camera.main;
        }

        public void Dispose(IPlayerContext context) => _characterSubscription.Dispose();

        private void OnCharacterChanged(IEntity character)
        {
            _rotateDirection = character?.GetRotateDirection();
            _characterTransform = character?.GetTransform();
        }

        private void Rotate()
        {
            if (_rotateDirection == null || _characterTransform == null) return;

            if (_camera == null)
            {
                // The camera can be destroyed on scene teardown; re-resolving is cheap enough
                // here because it only happens once the cached one is gone.
                _camera = UnityEngine.Camera.main;
                if (_camera == null) return;
            }

            Vector3 characterPosition = _characterTransform.position;
            Plane aimPlane = new Plane(Vector3.up, characterPosition);
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

            if (!aimPlane.Raycast(ray, out float distance)) return;

            Vector3 direction = ray.GetPoint(distance) - characterPosition;
            direction.y = 0f;

            // Cursor sitting on the character itself: keep the current facing.
            if (direction.sqrMagnitude < 0.0001f) return;

            _rotateDirection.Value = direction.normalized;
        }

        public void LateTick(IPlayerContext context, float deltaTime) => Rotate();
    }
}
