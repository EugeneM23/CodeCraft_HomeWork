using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank runs enemies down without slowing. Every physics step, before the simulation, it
    /// looks along its hull a step and a bit ahead: an enemy there, with the tank going faster than
    /// <see cref="TankCrushConfig.minSpeed"/>, dies before the hull touches it — no contact, so
    /// nothing takes the tank's speed (a Regax weighs ten tonnes and would stop it dead). The tank
    /// itself is the killer, so the voxel flesh (VoxelGibs) is thrown away from it; the flesh is
    /// loose and the tank drives through it (PassThroughLoosePiecesBehaviour).
    ///
    /// Blood bursts from under the tracks in three fans, out to either side and back along the
    /// tracks, harder the faster the tank went, and splashes onto the tank's voxel skin the way mud
    /// does, on the side the body burst against — the faces turned to it, thickest right there:
    /// wet voxels that come off the wheels again on dry ground (VoxelTrackRuts).
    ///
    /// Slower, the tank shoves the enemy or stops against it, as before; pressing on against it
    /// with the gas (the contacts) still crushes it, as TankRamBehaviour breaks a wall it pushes.
    /// The player's character is never crushed.
    /// </summary>
    public sealed class TankCrushBehaviour : IEntityInit, IEntityDispose, IEntityFixedTick
    {
        /// <summary>
        /// How far (m) past this step's travel the hull looks ahead, and how far below its collider:
        /// enough that the enemy dies a step before the hull would reach it.
        /// </summary>
        private const float LEAD = 0.5f;
        private const float BELOW = 0.4f;

        private static readonly Collider[] Hits = new Collider[64];
        /// <summary>Tank speed (m/s) at which the fans fly at their fastest.</summary>
        private const float FULL_SPEED = 10f;

        /// <summary>More than any body has: a crush always kills.</summary>
        private const int CRUSH_DAMAGE = 100000;

        /// <summary>How far down (m) from the contact the ground is looked for: the fans start just above it.</summary>
        private const float GROUND_PROBE = 3f;

        /// <summary>Lift of the side fans and of the back one: low sheets out, a higher tail behind.</summary>
        private const float SIDE_LIFT = 0.25f;
        private const float BACK_LIFT = 0.6f;

        /// <summary>Share of the tank's own velocity the drops carry.</summary>
        private const float INHERIT = 0.3f;

        /// <summary>Tries per splash to find a spot of skin facing the burst and in reach of it.</summary>
        private const int STAIN_TRIES = 16;

        /// <summary>
        /// A pressed contact counts as ahead when its normal, which points back at the tank, is at
        /// least this much against the direction of travel (as TankRamBehaviour has it).
        /// </summary>
        private const float AHEAD = 0.3f;

        /// <summary>The skin's parts and the running sum of their sizes, for picking one by its size.</summary>
        private static readonly List<VoxelBody> Parts = new();
        private static readonly List<float> Sizes = new();

        private readonly CollisionContactReceiver _receiver;
        private readonly Rigidbody _body;
        private readonly TankCrushConfig _config;

        private IEntity _crusher;
        private Color32[] _blood;
        private VoxelizedModel _skin;
        private IValue<Vector2> _input;
        private IValue<float> _maxSpeed;

        /// <summary>The hull's own box: the biggest solid box collider on the tank's body.</summary>
        private BoxCollider _hull;

        public TankCrushBehaviour(CollisionContactReceiver receiver, Rigidbody body, TankCrushConfig config)
        {
            _receiver = receiver;
            _body = body;
            _config = config;
        }

        public void Init(IEntity entity)
        {
            // The killer the crushed body's death reads: where it came from and a blow it cannot survive.
            _crusher = new Entity();
            _crusher.AddTransform(_body.transform);
            _crusher.AddDamage(new ReactiveInt(CRUSH_DAMAGE));

            _blood = _config.blood.ToVoxels();
            _skin = _body.GetComponent<VoxelizedModel>();
            _input = entity.TryGetDriveInput(out IReactiveVariable<Vector2> input) ? input : null;
            _maxSpeed = entity.TryGetMoveSpeed(out IReactiveVariable<float> speed) ? speed : null;
            _hull = BiggestBox(_body);

            _receiver.OnEntered += OnStruck;
            _receiver.OnStayed += OnPressing;
        }

        /// <summary>
        /// Before the physics step: an enemy in the hull's path dies before the hull touches it, so
        /// the tank keeps its speed.
        /// </summary>
        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (_hull == null || !_hull.enabled)
                return;

            Transform frame = _hull.transform;
            float forward = Vector3.Dot(_body.linearVelocity, frame.forward);
            float speed = Mathf.Abs(forward);
            if (speed < _config.minSpeed)
                return;

            // The hull's box, stretched forward (or back, reversing) over this step's travel and a
            // little more, and down to the ground under it.
            Vector3 half = Vector3.Scale(_hull.size, frame.lossyScale) * 0.5f;
            float lead = speed * deltaTime + LEAD;
            float sign = Mathf.Sign(forward);
            Vector3 centre = frame.TransformPoint(_hull.center)
                             + frame.forward * (sign * lead * 0.5f)
                             - frame.up * (BELOW * 0.5f);
            half.z += lead * 0.5f;
            half.y += BELOW * 0.5f;

            int count = Physics.OverlapBoxNonAlloc(centre, half, Hits, frame.rotation, 1 << PhysicsLayers.Enemy,
                QueryTriggerInteraction.Ignore);
            Vector3 nose = frame.TransformPoint(_hull.center) + frame.forward * (sign * half.z);
            for (int i = 0; i < count; i++)
            {
                Collider enemy = Hits[i];
                if (enemy != null && TryGetVictim(enemy.attachedRigidbody != null ? enemy.attachedRigidbody.gameObject : enemy.gameObject,
                        out IEntity victim))
                    Crush(victim, enemy.ClosestPoint(nose), speed);
            }
        }

        private static BoxCollider BiggestBox(Rigidbody body)
        {
            BoxCollider biggest = null;
            float volume = 0f;
            foreach (BoxCollider box in body.GetComponents<BoxCollider>())
            {
                if (box.isTrigger)
                    continue;
                Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                float v = Mathf.Abs(size.x * size.y * size.z);
                if (v > volume)
                {
                    volume = v;
                    biggest = box;
                }
            }

            return biggest;
        }

        public void Dispose(IEntity entity)
        {
            if (_receiver == null)
                return;
            _receiver.OnEntered -= OnStruck;
            _receiver.OnStayed -= OnPressing;
        }

        /// <summary>Running into the enemy: how fast the tank closes on it at the point of contact.</summary>
        private void OnStruck(Collision collision)
        {
            ContactPoint contact = collision.GetContact(0);
            Vector3 theirs = collision.rigidbody != null ? collision.rigidbody.linearVelocity : Vector3.zero;
            // The contact's normal points back at the tank: against it is the tank closing in.
            TryCrush(collision, contact, Vector3.Dot(_body.GetPointVelocity(contact.point) - theirs, -contact.normal));
        }

        /// <summary>
        /// Pressing against it: an enemy too heavy to shove (Regax weighs ten tonnes) stops the hull,
        /// and a stopped hull closes at no speed at all. What counts then is the speed the driver
        /// asks for, while the enemy is ahead — as TankRamBehaviour does for walls.
        /// </summary>
        private void OnPressing(Collision collision)
        {
            if (_input == null || _maxSpeed == null)
                return;

            float throttle = _input.Value.y;
            if (Mathf.Abs(throttle) < 0.1f)
                return;

            ContactPoint contact = collision.GetContact(0);
            Vector3 travel = _body.transform.forward * Mathf.Sign(throttle);
            if (Vector3.Dot(contact.normal, travel) > -AHEAD)
                return;

            TryCrush(collision, contact, Mathf.Abs(throttle) * _maxSpeed.Value);
        }

        private void TryCrush(Collision collision, ContactPoint contact, float speed)
        {
            GameObject other = collision.rigidbody != null ? collision.rigidbody.gameObject : collision.gameObject;
            if (speed >= _config.minSpeed && TryGetVictim(other, out IEntity victim))
                Crush(victim, contact.point, speed);
        }

        private void Crush(IEntity victim, Vector3 point, float speed)
        {
            DamageUseCase.TakeDamage(victim, new TakeDamageArgs(_crusher));
            if (IsAlive(victim))
                return;

            Vector3 under = Under(point);
            Spray(under, speed);
            Stain(point, under.y);
        }

        /// <summary>Just above the ground under the contact, where the fans start; the contact itself if no ground is found.</summary>
        private static Vector3 Under(Vector3 contact)
        {
            if (Physics.Raycast(contact + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, GROUND_PROBE,
                    1 << PhysicsLayers.Ground, QueryTriggerInteraction.Ignore))
                return ground.point + Vector3.up * 0.1f;
            return contact;
        }

        /// <summary>A living enemy: the body a touched collider belongs to.</summary>
        private static bool TryGetVictim(GameObject other, out IEntity victim)
        {
            if (!other.TryGetEntity(out victim) && !other.FindEntityInParent(out victim))
                return false;

            return victim.HasEnemyTag() && victim.HasDamageableTag() && IsAlive(victim);
        }

        // A dead body has been disposed (DeathBehaviour), or at least has no health left.
        private static bool IsAlive(IEntity entity) =>
            entity.TryGetHealth(out Health health) && !health.IsEmpty();

        private void Spray(Vector3 under, float speed)
        {
            Vector3 travel = Vector3.ProjectOnPlane(_body.linearVelocity, Vector3.up);
            if (travel.sqrMagnitude < 0.01f)
                travel = Vector3.ProjectOnPlane(_body.transform.forward, Vector3.up);
            travel.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, travel);

            float fast = Mathf.Lerp(_config.slowSpray, _config.fastSpray, Mathf.Clamp01(speed / FULL_SPEED));
            float slow = fast * 0.6f;
            Vector3 inherit = _body.linearVelocity * INHERIT;
            float size = _config.blood.dropSize;

            VoxelSpray.Burst(under, side + Vector3.up * SIDE_LIFT, _config.dropsPerFan, slow, fast, _config.spread, _blood, size, inherit);
            VoxelSpray.Burst(under, -side + Vector3.up * SIDE_LIFT, _config.dropsPerFan, slow, fast, _config.spread, _blood, size, inherit);
            VoxelSpray.Burst(under, -travel + Vector3.up * BACK_LIFT, _config.dropsPerFan, slow, fast, _config.spread, _blood, size, inherit);
        }

        /// <summary>
        /// Blood from the burst body splashed onto the skin the way VoxelTrackRuts splashes mud:
        /// wet voxels laid on, the plate stained — on the side of the tank the body burst against,
        /// nowhere else. A spot of skin takes blood only if it faces the burst, thickest right at
        /// it and none past <see cref="TankCrushConfig.splashReach"/>, thinner high up. Parts in
        /// reach take splashes by their size (the 2/3 power of their voxels, about their surface),
        /// so a thin gun over the burst takes as much as its size says, not one part's share; and a
        /// splash sticks to the face it hits, not round the far side of a thin part.
        /// </summary>
        private void Stain(Vector3 burst, float groundY)
        {
            if (_skin == null || _skin.Bodies.Count == 0 || _blood.Length == 0)
                return;

            float reach = Mathf.Max(0.1f, _config.splashReach);
            Parts.Clear();
            Sizes.Clear();
            float total = 0f;
            foreach (VoxelBody part in _skin.Bodies)
            {
                if (part == null || !part.IsCreated || part.SolidCount == 0 ||
                    part.WorldBounds.SqrDistance(burst) > reach * reach)
                    continue;
                total += Mathf.Pow(part.SolidCount, 2f / 3f);
                Parts.Add(part);
                Sizes.Add(total);
            }
            if (Parts.Count == 0)
                return;

            for (int i = 0; i < _config.splashes; i++)
            {
                for (int t = 0; t < STAIN_TRIES; t++)
                {
                    VoxelBody part = Pick(Random.value * total);
                    if (!part.TryGetSurfacePoint(2f, out Vector3 at, out Vector3 normal))
                        continue;

                    if (!TryGetStainChance(at, normal, burst, groundY, reach, _config.thickBelow, out float keep))
                        continue;
                    if (Random.value > keep)
                        continue;

                    // The splash's middle stands off the face, so it covers this side only.
                    Vector3 middle = at + normal * (_config.stainRadius * 0.6f);
                    Color32 colour = _blood[i % _blood.Length];
                    part.Deposit(middle, _config.stainRadius, colour, _config.stainFill);
                    part.Paint(middle, _config.stainRadius * 1.4f, colour, 0.35f);
                    break;
                }
            }

            Parts.Clear();
            Sizes.Clear();
        }

        /// <summary>
        /// The chance (at most 1; zero or less means never) that a splash of the burst at
        /// <paramref name="burst"/> sticks to a spot of skin at <paramref name="at"/> facing
        /// <paramref name="normal"/>. False, with no chance at all, past <paramref name="reach"/>.
        /// </summary>
        public static bool TryGetStainChance(Vector3 at, Vector3 normal, Vector3 burst, float groundY, float reach,
            float thickBelow, out float chance)
        {
            chance = 0f;
            Vector3 toBurst = burst - at;
            float distance = toBurst.magnitude;
            if (distance >= reach)
                return false;

            // A face turned away from the burst stays clean; a part one voxel thin (no normal) and a
            // spot at the burst itself take it from any side.
            float facing = normal == Vector3.zero || distance < 0.2f
                ? 1f
                : Vector3.Dot(normal, toBurst / distance);
            // Falling off steeply: a face looking straight at the burst from the far side of the nose
            // must not outdo the near side, which meets it at a slant.
            float near = 1f - distance / reach;
            chance = near * near * facing * Thin(at.y - groundY, thickBelow);
            return true;
        }

        /// <summary>The part whose stretch of the running sum of sizes holds <paramref name="value"/>.</summary>
        private static VoxelBody Pick(float value)
        {
            for (int i = 0; i < Parts.Count; i++)
                if (value < Sizes[i])
                    return Parts[i];
            return Parts[Parts.Count - 1];
        }

        /// <summary>Share of splashes kept at a height: all below <paramref name="thick"/>, (thick / value)² above it.</summary>
        private static float Thin(float value, float thick)
        {
            thick = Mathf.Max(0.01f, thick);
            return value <= thick ? 1f : thick * thick / (value * value);
        }
    }
}
