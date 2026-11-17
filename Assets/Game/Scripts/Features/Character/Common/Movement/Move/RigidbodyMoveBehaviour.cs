using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Drives the entity through its Rigidbody instead of writing the transform directly, and
    /// slides along whatever it runs into instead of stopping dead.
    ///
    /// The transform-writing <see cref="TransformMoveBehaviour"/> is still the right choice for
    /// entities that have no Rigidbody (bullets, shells, enemies), and is left untouched for them.
    /// For a body with colliders a transform write is a pose teleport: PhysX does not sweep it, so
    /// nothing stops the capsule entering geometry, and the solver then pushes the body back out
    /// and writes that pose onto the same transform — two authorities over one position, which is
    /// what produced the jitter against walls.
    ///
    /// Sliding is done here rather than left to the solver. Handing the solver a velocity that
    /// points into a wall makes it cancel the normal component, and the contact force that does so
    /// also drives Coulomb friction, which eats the tangential component as well — the character
    /// stops dead. Casting the capsule first and keeping only the along-the-wall component means
    /// the velocity handed over is already legal, so there is almost no contact force and almost
    /// nothing for friction to bite on.
    /// </summary>
    public sealed class RigidbodyMoveBehaviour : IEntityInit, IEntityFixedTick
    {
        /// <summary>
        /// How much of the along-the-wall speed is kept. 1 is a pure geometric slide: pressing
        /// straight into a wall gives zero, grazing it keeps nearly full speed, and anything
        /// between scales with the cosine of the angle. Lower it to make walls feel draggy.
        /// </summary>
        private const float SLIDE_RETENTION = 1f;

        /// <summary>Small margin so the cast still sees a wall the capsule is already touching.</summary>
        private const float SKIN = 0.02f;

        /// <summary>Planes resolved per step: a wall, then the corner it forms with a second wall.</summary>
        private const int MAX_PLANES = 2;

        /// <summary>
        /// A ledge no higher than this above the capsule's foot is stepped over, not slid along: the
        /// rim of a grenade crater (the destructible floor is 4 cm thick) or a chip of floor lying
        /// flat. The cast meets such a ledge with the rounded bottom of the capsule, and flattening
        /// that normal made it a full wall, so a character in a crater could not walk out of it.
        /// Left to the solver, the rounded foot rides up over the edge.
        /// </summary>
        private const float STEP_HEIGHT = 0.06f;

        /// <summary>
        /// What the character can be blocked by: not itself (9 Character), not the things flying
        /// around it — 11 ThrowItem, 12 Bullet, 13 Weapon — and not pickups (6 Interactible).
        /// Layer names: ProjectSettings/TagManager.asset.
        ///
        /// Pickups have to be excluded: health kits, ammo and dropped weapons all sit on
        /// Interactible with solid box colliders, so the cast read them as walls and walking into
        /// one zeroed the velocity outright. Leaving them out of the cast hands the solver a
        /// velocity that points into the box, and because this behaviour gives the capsule a
        /// frictionless material the normal component is cancelled while the tangential one
        /// survives — the character slides past the item instead of sticking to it.
        /// </summary>
        private const int BLOCKING_MASK = ~((1 << PhysicsLayers.Interactable) | (1 << PhysicsLayers.Character) | (1 << PhysicsLayers.ThrowItem) | (1 << PhysicsLayers.Bullet) | (1 << PhysicsLayers.Weapon));

        // Every hit along the sweep, not just the first: the nearest may be a piece the capsule
        // passes through, with a real wall behind it. Next to a broken wall the sweep crosses a
        // good number of pieces.
        private static readonly RaycastHit[] Hits = new RaycastHit[64];

        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private IValue<Vector3> _moveDirection;
        private IValue<float> _moveSpeed;
        private IExpression<bool> _moveCondition;

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _moveDirection = entity.GetMoveDirection();
            _moveSpeed = entity.GetMoveSpeed();

            // Optional: enemies and some content entities install no condition.
            entity.TryGetMoveCondition(out _moveCondition);

            _body.TryGetComponent(out _capsule);

            // The project default is 10 m/s (ProjectSettings/DynamicsManager.asset,
            // m_DefaultMaxDepenetrationVelocity). When the solver finds the capsule overlapping
            // geometry it pushes it out at up to that speed, and 10 m/s straight up reaches about
            // five metres — which is exactly what a dash into a wall looked like. Capping it here
            // turns the same correction into a few centimetres.
            _body.maxDepenetrationVelocity = 1f;

            if (_capsule != null && _capsule.sharedMaterial == null)
            {
                // No PhysicsMaterial anywhere in this project, so the capsule inherits the built-in
                // 0.6/0.6 default and walls grab it. A character should not be held by friction;
                // Minimum makes this win over whatever the wall carries.
                _capsule.sharedMaterial = new PhysicsMaterial("CharacterFrictionless")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            // A blocked or dead character brakes to a stop. Returning early instead would leave the
            // last velocity applied and let it glide on.
            bool allowed = _moveCondition == null || _moveCondition.Value;

            Vector3 direction = _moveDirection.Value;
            direction.y = 0f;

            Vector3 desired = allowed ? direction * _moveSpeed.Value : Vector3.zero;

            if (desired.sqrMagnitude > 0f)
                desired = Slide(desired, deltaTime);

            Vector3 current = _body.linearVelocity;
            Vector3 velocity = current;
            velocity.x = desired.x;
            velocity.z = desired.z;

            // Contacts must not launch the capsule. A jump is the only thing allowed to drive it up
            // hard: JumpUseCase applies an impulse, so the most it can produce is jumpForce / mass.
            // Anything beyond that came from the solver clearing an overlap, and since this capsule
            // is frictionless and the body has no drag, nothing bleeds it off again — it just flies.
            //
            // The floor matters as much as the cap. Clamping to zero while no jump is available
            // would take away the solver's only way of pushing the capsule up out of geometry, and
            // a capsule that cannot be extracted sinks instead of hopping — trading a visible bug
            // for a worse, quieter one. maxDepenetrationVelocity is exactly that allowance: it is
            // what the solver is permitted to use, and at 1 m/s it lifts about five centimetres.
            float jumpCeiling = entity.TryGetJumpForce(out float jumpForce)
                ? jumpForce / Mathf.Max(_body.mass, 0.0001f)
                : 0f;

            float ceiling = Mathf.Max(jumpCeiling, _body.maxDepenetrationVelocity);

            if (velocity.y > ceiling)
                velocity.y = ceiling;

            // Only a change is written. Writing a body's velocity wakes it, even with the value it
            // already has: a character standing still, and the unridden mount, were kept awake by
            // it every step. Left alone they fall asleep, and a key press, a hit or a push wakes them.
            if (!velocity.Equals(current))
                _body.linearVelocity = velocity;
        }

        /// <summary>
        /// Projects the wanted velocity onto the surfaces in its way, so what comes out runs along
        /// them. Returns zero only when the motion is genuinely boxed in.
        /// </summary>
        private Vector3 Slide(Vector3 desired, float deltaTime)
        {
            if (_capsule == null) return desired;

            for (int plane = 0; plane < MAX_PLANES; plane++)
            {
                float speed = desired.magnitude;
                if (speed < 0.0001f) return Vector3.zero;

                Vector3 heading = desired / speed;
                if (!CastBlocking(_capsule, heading, speed * deltaTime + SKIN, BLOCKING_MASK, out RaycastHit hit))
                    break;

                if (hit.point.y - FootHeight() <= STEP_HEIGHT) break;

                // Flatten the normal: floors and ceilings must not steal horizontal speed, and
                // gravity already owns the vertical axis.
                Vector3 normal = hit.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude < 0.0001f) break;
                normal.Normalize();

                // Already moving away from this surface: nothing to clip.
                float into = Vector3.Dot(desired, normal);
                if (into >= 0f) break;

                desired = (desired - normal * into) * SLIDE_RETENTION;
                desired.y = 0f;
            }

            return desired;
        }

        /// <summary>World height of the bottom of the capsule. The body's rotation is frozen upright.</summary>
        private float FootHeight() =>
            _capsule.transform.TransformPoint(_capsule.center).y - _capsule.height * 0.5f;

        /// <summary>
        /// The nearest surface the capsule would run into within <paramref name="distance"/> along
        /// <paramref name="heading"/>. Only what it actually collides with counts: a query does not
        /// know about Physics.IgnoreCollision, so a knocked-out brick the character passes through
        /// (PassThroughLoosePiecesBehaviour switches those pairs off) would otherwise read as a wall
        /// and stop it in front of rubble it walks through.
        /// </summary>
        public static bool CastBlocking(CapsuleCollider capsule, Vector3 heading, float distance, int mask,
            out RaycastHit hit)
        {
            Transform capsuleTransform = capsule.transform;
            float radius = capsule.radius;
            float half = Mathf.Max(0f, capsule.height * 0.5f - radius);

            Vector3 centre = capsuleTransform.TransformPoint(capsule.center);
            Vector3 axis = capsuleTransform.up * half;

            // Slightly narrower than the real capsule so a surface it is already resting against
            // does not register as a blocking hit every single step.
            int count = Physics.CapsuleCastNonAlloc(
                centre - axis,
                centre + axis,
                radius * 0.95f,
                heading,
                Hits,
                distance,
                mask,
                QueryTriggerInteraction.Ignore
            );

            hit = default;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = Hits[i];

                // Overlapping at the start of the sweep: reported at distance 0 here, never by a
                // single cast, which this replaces.
                if (candidate.distance <= 0f)
                    continue;

                Collider other = candidate.collider;
                if (other == capsule || Physics.GetIgnoreCollision(capsule, other))
                    continue;

                if (!found || candidate.distance < hit.distance)
                {
                    hit = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
