using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// The player's vehicle as the level's AI sees it: the transform to chase, whether it is alive, and its hull — the
    /// biggest solid box collider on its root body (as <c>TankCrushBehaviour</c> takes it), a box in the hull's frame.
    /// The hull answers "how far is the hull from me" (the bite's reach, like the old AI's distance to the vehicle's
    /// bounds) and "push me out of the hull" (an elite stands against the tank, not in it).
    /// <see cref="Refresh"/> reads the player's current character from the game context once a frame; the binding
    /// itself (components looked up) happens only when the character changes.
    /// </summary>
    public sealed class PlayerTank
    {
        /// <summary>Height above the root the eyes look at when there is no hull.</summary>
        private const float AIM_HEIGHT = 1f;

        private IEntity _entity;
        private Transform _root;
        private BoxCollider _hull;
        private Health _health;

        /// <summary>The vehicle's root, or null when the player has none (or it is gone).</summary>
        public Transform Transform => _root != null && _root.gameObject.activeInHierarchy ? _root : null;

        public IEntity Entity => _entity;

        /// <summary>The hull box (null — none: distances are taken to the root).</summary>
        public BoxCollider Hull => _hull;

        /// <summary>There is a vehicle to fight, and it is alive.</summary>
        public bool IsPresent => Transform != null && (_health == null || !_health.IsEmpty());

        /// <summary>The middle of the hull: where eyes look and where a shot would hit.</summary>
        public Vector3 AimPoint =>
            _hull != null ? _hull.transform.TransformPoint(_hull.center) : _root != null ? _root.position + Vector3.up * AIM_HEIGHT : Vector3.zero;

        /// <summary>Reads the player's character from the game context; binds it when it changed.</summary>
        public void Refresh()
        {
            GameContext context = GameContext.Instance;
            IEntity current = context != null && context.TryGetPlayerContext(out PlayerContext player)
                              && player.TryGetCharacter(out IReactiveVariable<IEntity> character)
                ? character.Value
                : null;
            if (!ReferenceEquals(current, _entity))
                Bind(current);
        }

        /// <summary>Binds a vehicle entity (its transform from the component or its Transform value). Null — none.</summary>
        public void Bind(IEntity entity)
        {
            _entity = entity;
            _health = null;
            if (entity == null)
            {
                BindRoot(null);
                return;
            }

            Transform root = entity is Component component ? component.transform
                : entity.TryGetTransform(out Transform t) ? t : null;
            BindRoot(root);
            if (entity.TryGetHealth(out Health health))
                _health = health;
        }

        /// <summary>Binds a bare transform (tests, tools): its hull is the biggest solid box on its root body.</summary>
        public void Bind(Transform root)
        {
            _entity = null;
            _health = null;
            BindRoot(root);
        }

        /// <summary>Flat distance (XZ) from a point to the hull's box; to the root when there is no hull.</summary>
        public float DistanceToHull(Vector3 point)
        {
            if (_hull == null)
            {
                if (_root == null)
                    return float.PositiveInfinity;
                Vector3 d = point - _root.position;
                d.y = 0f;
                return d.magnitude;
            }

            HullFrame(out Vector3 center, out Vector3 right, out Vector3 forward, out Vector2 half);
            Vector3 offset = point - center;
            float dx = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(offset, right)) - half.x);
            float dz = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(offset, forward)) - half.y);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Moves a body of <paramref name="radius"/> standing at <paramref name="position"/> out of the hull (flat, along
        /// the side it is nearest to). True — it was in.
        /// </summary>
        public bool PushOut(ref Vector3 position, float radius)
        {
            if (_hull == null || !IsPresent)
                return false;

            HullFrame(out Vector3 center, out Vector3 right, out Vector3 forward, out Vector2 half);
            Vector3 offset = position - center;
            float x = Vector3.Dot(offset, right);
            float z = Vector3.Dot(offset, forward);
            float hx = half.x + radius;
            float hz = half.y + radius;
            if (Mathf.Abs(x) >= hx || Mathf.Abs(z) >= hz)
                return false;

            if (hx - Mathf.Abs(x) < hz - Mathf.Abs(z))
                x = x >= 0f ? hx : -hx;
            else
                z = z >= 0f ? hz : -hz;

            float y = position.y;
            position = center + right * x + forward * z;
            position.y = y;
            return true;
        }

        private void BindRoot(Transform root)
        {
            _root = root;
            _hull = null;
            if (root == null)
                return;

            Rigidbody body = root.GetComponentInParent<Rigidbody>();
            GameObject owner = body != null ? body.gameObject : root.gameObject;
            float volume = 0f;
            foreach (BoxCollider box in owner.GetComponents<BoxCollider>())
            {
                if (box.isTrigger)
                    continue;
                Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                float v = Mathf.Abs(size.x * size.y * size.z);
                if (v <= volume)
                    continue;
                volume = v;
                _hull = box;
            }
        }

        // The hull's box flat on the ground: its middle, its sideways and forward axes, its half width and half length
        private void HullFrame(out Vector3 center, out Vector3 right, out Vector3 forward, out Vector2 half)
        {
            Transform frame = _hull.transform;
            center = frame.TransformPoint(_hull.center);
            Vector3 scale = frame.lossyScale;
            half = new Vector2(Mathf.Abs(_hull.size.x * scale.x), Mathf.Abs(_hull.size.z * scale.z)) * 0.5f;
            right = frame.right;
            forward = frame.forward;
            right.y = 0f;
            forward.y = 0f;
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }
    }
}
