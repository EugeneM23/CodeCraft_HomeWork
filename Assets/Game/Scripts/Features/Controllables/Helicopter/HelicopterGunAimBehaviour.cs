using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Points every gun of the helicopter at the cursor on the ground. Bullets leave along the
    /// fire point's forward (SpawnUseCase.SpawnBullet), so from cruise height a level gun would
    /// shoot over every zombie's head; these dip to where the cursor is. Each gun turns from its
    /// own mount, so guns on either side converge on the same point.
    ///
    /// The pivots hang from the root, not from the leaning body: the stream of fire does not
    /// wobble with the tilt. Each visible gun, inside the body, copies its pivot's turn relative
    /// to the body.
    /// </summary>
    public sealed class HelicopterGunAimBehaviour : IEntityInit, IEntityTick
    {
        private readonly Transform[] _pivots;
        private readonly Transform[] _visuals;
        private readonly float _speed;
        private readonly float _maxYaw;
        private readonly float _maxPitchDown;

        private Transform _root;
        private IValue<Vector3> _aimPoint;

        // Per pivot: the rotation last written, and what the pivot and its visual held right after.
        // The same turn again, onto a pivot and a visual nobody has touched since, skips both writes:
        // a parked helicopter's guns hold their aim, and rewriting them every frame only marked them
        // changed. Zero quaternions to start with, never a rotation, so the first frame always writes.
        private readonly Quaternion[] _written;
        private readonly Quaternion[] _held;
        private readonly Quaternion[] _shown;

        /// <param name="visuals">Same order as <paramref name="pivots"/>; may be shorter or hold nulls.</param>
        public HelicopterGunAimBehaviour(Transform[] pivots, Transform[] visuals, float speed, float maxYaw,
            float maxPitchDown)
        {
            _pivots = pivots ?? new Transform[0];
            _visuals = visuals ?? new Transform[0];
            _speed = speed;
            _maxYaw = maxYaw;
            _maxPitchDown = maxPitchDown;
            _written = new Quaternion[_pivots.Length];
            _held = new Quaternion[_pivots.Length];
            _shown = new Quaternion[_pivots.Length];
        }

        public void Init(IEntity entity)
        {
            _root = entity.GetTransform();
            _aimPoint = entity.GetAimPoint();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            for (int i = 0; i < _pivots.Length; i++)
            {
                Transform pivot = _pivots[i];
                if (pivot == null)
                    continue;

                Quaternion current = pivot.localRotation;
                Quaternion turned = Quaternion.RotateTowards(current, Target(pivot), _speed * deltaTime);
                Transform visual = i < _visuals.Length ? _visuals[i] : null;

                if (turned.Equals(_written[i]) && current.Equals(_held[i]) &&
                    (visual == null || visual.localRotation.Equals(_shown[i])))
                    continue;

                pivot.localRotation = turned;
                _written[i] = turned;
                _held[i] = pivot.localRotation;

                if (visual != null)
                {
                    visual.localRotation = _held[i];
                    _shown[i] = visual.localRotation;
                }
            }
        }

        private Quaternion Target(Transform pivot)
        {
            Vector3 local = Quaternion.Inverse(_root.rotation) * (_aimPoint.Value - pivot.position);
            float flat = new Vector2(local.x, local.z).magnitude;

            if (flat < 1e-3f && Mathf.Abs(local.y) < 1e-3f)
                return Quaternion.identity;

            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -_maxYaw, _maxYaw);
            float pitch = Mathf.Clamp(Mathf.Atan2(-local.y, flat) * Mathf.Rad2Deg, 0f, _maxPitchDown);
            return Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
