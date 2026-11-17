using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class MoveUseCase
    {
        public static void MoveSelf(this IEntity entity, float deltaTime)
        {
            IReactiveValue<Vector3> direction = entity.GetMoveDirection();
            entity.Move(direction.Value, deltaTime);
        }

        public static void Move(this IEntity entity, in Vector3 direction, in float deltaTime)
        {
            if (entity.TryGetMoveCondition(out var condition) && !condition.Value) return;

            Transform transform = entity.GetTransform();
            float speed = entity.GetMoveSpeed().Value;

            transform.position += direction * speed * deltaTime;
        }

        // Small overlap so the physics step that follows reports the contact.
        private const float SWEEP_SKIN = 0.01f;

        /// <summary>
        /// Same as <see cref="MoveSelf"/>, but for fast bodies moved by transform: sweeps the body's
        /// colliders along the step first and stops at the first obstacle, so a step longer than a
        /// thin wall does not jump over it. The sweep honours the layer collision matrix.
        /// </summary>
        public static void MoveSelfSwept(this IEntity entity, Rigidbody body, float deltaTime)
        {
            if (entity.TryGetMoveCondition(out var condition) && !condition.Value) return;

            Vector3 step = entity.GetMoveDirection().Value * entity.GetMoveSpeed().Value * deltaTime;
            Collider own = body != null ? body.GetComponent<Collider>() : null;
            MoveSwept(entity.GetTransform(), body, own, step);
        }

        /// <summary>
        /// The move of <see cref="MoveSelfSwept"/> with everything already looked up, for
        /// <see cref="BulletMoveBehaviour"/>, which runs it for every bullet on every physics step.
        /// <paramref name="own"/> is the body's first collider, as GetComponent finds it: the one an
        /// ignored pair is asked about. Null treats every hit as a real obstacle.
        /// </summary>
        public static void MoveSwept(Transform transform, Rigidbody body, Collider own, Vector3 step)
        {
            float distance = step.magnitude;
            if (distance < 0.0001f) return;

            Vector3 direction = step / distance;
            if (body != null && SweepToObstacle(body, own, direction, distance, out float hitDistance))
                distance = hitDistance + SWEEP_SKIN;

            transform.position += direction * distance;
        }

        /// <summary>
        /// Distance to the nearest collider the body can actually hit. A sweep does not know about
        /// Physics.IgnoreCollision: a bullet leaving a gun mounted on a hull stopped against the
        /// hull it passes through. Only when the nearest hit is such a pair are all hits looked at.
        /// </summary>
        private static bool SweepToObstacle(Rigidbody body, Collider own, Vector3 direction, float distance,
            out float hitDistance)
        {
            hitDistance = 0f;

            if (!body.SweepTest(direction, out RaycastHit hit, distance, QueryTriggerInteraction.Ignore))
                return false;

            if (own == null || !Physics.GetIgnoreCollision(own, hit.collider))
            {
                hitDistance = hit.distance;
                return true;
            }

            return NearestNotIgnored(body, own, direction, distance, out hitDistance);
        }

        // Every hit along the sweep, not just the nearest. Next to a broken wall the bullet's sphere
        // crosses a good number of pieces.
        private static readonly RaycastHit[] SweepHits = new RaycastHit[64];

        /// <summary>
        /// The nearest hit along the sweep that is not an ignored pair. Rigidbody.SweepTestAll found
        /// it in a new array on every call — every step of every round still inside the hull of
        /// the vehicle that fired it. The same sweep is cast here into a reused buffer: the body's own sphere,
        /// from its physics pose, through the layers its layer collides with, its own colliders left
        /// out as the body sweep leaves them out. A collider of another shape, or a buffer that comes
        /// back full and may have left the nearest hit out, still goes through SweepTestAll.
        /// </summary>
        private static bool NearestNotIgnored(Rigidbody body, Collider own, Vector3 direction, float distance,
            out float hitDistance)
        {
            hitDistance = float.MaxValue;
            bool found = false;

            if (SweepAsSphere(body, own, direction, distance, out int count))
            {
                for (int i = 0; i < count; i++)
                {
                    RaycastHit other = SweepHits[i];

                    if (other.rigidbody == body)
                        continue;

                    if (other.distance >= hitDistance || Physics.GetIgnoreCollision(own, other.collider))
                        continue;

                    hitDistance = other.distance;
                    found = true;
                }

                return found;
            }

            foreach (RaycastHit other in body.SweepTestAll(direction, distance, QueryTriggerInteraction.Ignore))
            {
                if (other.distance >= hitDistance || Physics.GetIgnoreCollision(own, other.collider))
                    continue;

                hitDistance = other.distance;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Sweeps <paramref name="own"/> into <see cref="SweepHits"/> when it is a sphere on the body's
        /// own transform — every bullet's collider. False, with nothing cast, otherwise, and when the
        /// buffer came back full.
        /// </summary>
        private static bool SweepAsSphere(Rigidbody body, Collider own, Vector3 direction, float distance,
            out int count)
        {
            count = 0;

            SphereCollider sphere = own as SphereCollider;
            if (sphere == null)
                return false;

            Transform shape = sphere.transform;
            if (shape != body.transform)
                return false;

            // A sphere collider is scaled by the largest axis of its transform's scale.
            Vector3 scale = shape.lossyScale;
            float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Vector3 centre = body.position + body.rotation * Vector3.Scale(sphere.center, scale);

            count = Physics.SphereCastNonAlloc(centre, radius, direction, SweepHits, distance,
                LayersHitBy(body.gameObject.layer), QueryTriggerInteraction.Ignore);

            return count < SweepHits.Length;
        }

        /// <summary>The layers <paramref name="layer"/> collides with, from the layer collision matrix.</summary>
        private static int LayersHitBy(int layer)
        {
            int mask = 0;
            for (int other = 0; other < 32; other++)
                if (!Physics.GetIgnoreLayerCollision(layer, other))
                    mask |= 1 << other;

            return mask;
        }
    }
}