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
        // The kill (damage, death, voxel gibs) and the tank's spray and stain, apart (perf debt of the crush frame).
        private static readonly Unity.Profiling.ProfilerMarker KillMarker = new("Crush.Kill");
        private static readonly Unity.Profiling.ProfilerMarker SprayMarker = new("Crush.SprayAndStain");
        private static readonly Unity.Profiling.ProfilerMarker StainMarker = new("Crush.StainStep");

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

        /// <summary>Splashes laid on the skin per physics step (about 0.15 ms each): see <see cref="Stain"/>.</summary>
        private const int STAINS_PER_STEP = 12;

        /// <summary>Crushes whose stain is still being laid; more at once and the oldest gives way.</summary>
        private const int PENDING_STAINS = 8;

        /// <summary>A crush's stain still being laid: the burst in the tank's frame, its height over the ground, the next splash.</summary>
        private struct PendingStain
        {
            public Vector3 LocalBurst;
            public float Height;
            public int Next;
        }

        private readonly PendingStain[] _pending = new PendingStain[PENDING_STAINS];
        private int _pendingCount;
        private bool _stainsReserved;

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

        /// <summary>The crusher's blow, one for every crush: the same source and factor each time, so none is made per kill.</summary>
        private TakeDamageArgs _crush;
        private Color32[] _blood;
        private VoxelizedModel _skin;
        private IValue<Vector2> _input;
        private IValue<float> _maxSpeed;

        /// <summary>The hull's own box: the biggest solid box collider on the tank's body.</summary>
        private BoxCollider _hull;

        /// <summary>The tank's own colliders: a knocked-down body lets them pass over it (KnockdownBehaviour).</summary>
        private Collider[] _vehicle;

        public TankCrushBehaviour(CollisionContactReceiver receiver, Rigidbody body, TankCrushConfig config)
        {
            _receiver = receiver;
            _body = body;
            _config = config;
            _minSpeed = config.minSpeed;
        }

        private float _minSpeed;
        private float _widthFactor = 1f;

        /// <summary>
        /// Slowest (m/s) the tank closes on an enemy and still crushes it. Starts at
        /// <see cref="TankCrushConfig.minSpeed"/>; the loadout (tracks) sets it.
        /// </summary>
        public float MinSpeed
        {
            get => _minSpeed;
            set => _minSpeed = Mathf.Max(0f, value);
        }

        /// <summary>Multiplier of the width of the box swept ahead for enemies to crush; the loadout (tracks) sets it.</summary>
        public float WidthFactor
        {
            get => _widthFactor;
            set => _widthFactor = Mathf.Max(0.1f, value);
        }

        public void Init(IEntity entity)
        {
            // The killer the crushed body's death reads: where it came from and a blow it cannot survive.
            _crusher = new Entity();
            _crusher.AddTransform(_body.transform);
            _crusher.AddDamage(new ReactiveInt(CRUSH_DAMAGE));
            _crush = new TakeDamageArgs(_crusher);

            _blood = _config.blood.ToVoxels();
            _skin = _body.GetComponent<VoxelizedModel>();
            _input = entity.TryGetDriveInput(out IReactiveVariable<Vector2> input) ? input : null;
            _maxSpeed = entity.TryGetMoveSpeed(out IReactiveVariable<float> speed) ? speed : null;
            _hull = BiggestBox(_body);
            _vehicle = _body.GetComponentsInChildren<Collider>(true);

            _receiver.OnEntered += OnStruck;
            _receiver.OnStayed += OnPressing;
        }

        /// <summary>
        /// Before the physics step: an enemy in the hull's path dies before the hull touches it, so
        /// the tank keeps its speed.
        /// </summary>
        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (!_stainsReserved && _skin != null && _skin.Bodies.Count > 0)
            {
                // Once the skin is built (VoxelizedModel, at Start): the first crush allocates nothing for its stain.
                _stainsReserved = true;
                IReadOnlyList<VoxelBody> parts = _skin.Bodies;
                for (int i = 0; i < parts.Count; i++)
                    if (parts[i] != null)
                        parts[i].ReserveStains();
            }

            if (_pendingCount > 0)
            {
                StainMarker.Begin();
                StainStep();
                StainMarker.End();
            }

            if (_hull == null || !_hull.enabled)
                return;

            CrushTheDown();

            Transform frame = _hull.transform;
            float forward = Vector3.Dot(_body.linearVelocity, frame.forward);
            float speed = Mathf.Abs(forward);
            if (speed < _minSpeed)
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
            half.x *= _widthFactor;

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
            if (speed >= _minSpeed && TryGetVictim(other, out IEntity victim))
                Crush(victim, contact.point, speed);
        }

        private void Crush(IEntity victim, Vector3 point, float speed)
        {
            // Run into: a body that can fall is knocked down first (the user); the hull rolling onto it pops it (CrushTheDown).
            if (victim.TryGetBehaviour(out KnockdownBehaviour knockdown))
            {
                if (!knockdown.IsDown)
                    knockdown.Knock(_vehicle);
                return;
            }

            Kill(victim, point, speed);
        }

        /// <summary>The crush itself: the body dies, the blood bursts from under the tracks and splashes the side it burst on.</summary>
        private void Kill(IEntity victim, Vector3 point, float speed)
        {
            KillMarker.Begin();
            DamageUseCase.TakeDamage(victim, _crush);
            KillMarker.End();
            if (IsAlive(victim))
                return;
            GameSignals.RaiseCrushed(point, speed);

            SprayMarker.Begin();
            Vector3 under = Under(point);
            Spray(under, speed);
            Stain(point, under.y);
            SprayMarker.End();
        }

        /// <summary>A knocked-down body pops when its middle is this far (m) inside the hull's footprint: under the tank.</summary>
        private const float COVERED = 0.3f;

        /// <summary>
        /// The knocked-down bodies (KnockdownBehaviour.Down) the hull is over now: popped from under the tracks, at the
        /// tank's speed (the blood fans as in a crush), however slowly it rolls onto them.
        /// </summary>
        private void CrushTheDown()
        {
            List<KnockdownBehaviour> down = KnockdownBehaviour.Down;
            if (down.Count == 0)
                return;

            Transform frame = _hull.transform;
            Vector3 half = Vector3.Scale(_hull.size, frame.lossyScale) * 0.5f;
            float speed = Mathf.Max(_minSpeed, Vector3.ProjectOnPlane(_body.linearVelocity, Vector3.up).magnitude);
            for (int i = down.Count - 1; i >= 0; i--)
            {
                if (i >= down.Count)
                    continue;
                KnockdownBehaviour body = down[i];
                Vector3 local = frame.InverseTransformPoint(body.Position) - _hull.center;
                local = Vector3.Scale(local, frame.lossyScale);
                bool under = Mathf.Abs(local.x) < half.x - COVERED && Mathf.Abs(local.z) < half.z - COVERED &&
                             local.y < half.y && local.y > -half.y - BELOW - 1f;
                if (!under || !IsAlive(body.Entity))
                    continue;
                // Its voxel death reads this at once (GibRules.ReadHit, KillingHit.UnderHull): flesh and blood out low, from under the tracks.
                KillingHit.CrushingUnderHull = true;
                try
                {
                    KillUnderHull(body.Entity, body.Position, speed, frame.TransformPoint(_hull.center), frame.rotation, half);
                }
                finally
                {
                    KillingHit.CrushingUnderHull = false;
                }
            }
        }

        /// <summary>
        /// A knocked-down body popped under the hull: it dies (its voxel flesh stays out of sight, KillingHit.UnderHull), and
        /// its blood and bits burst out from under the belly to every side as hard as an HE shell's hit (VoxelSpray.UnderHull):
        /// from the footprint's edge, flat, so nothing comes up through the hull or lands on it. Only the tracks take blood.
        /// </summary>
        private void KillUnderHull(IEntity victim, Vector3 point, float speed, Vector3 hullCenter, Quaternion hullRotation, Vector3 half)
        {
            KillMarker.Begin();
            DamageUseCase.TakeDamage(victim, _crush);
            KillMarker.End();
            if (IsAlive(victim))
                return;
            GameSignals.RaiseCrushed(point, speed);

            SprayMarker.Begin();
            Vector3 under = Under(point);
            VoxelSpray.UnderHull(point, hullCenter, hullRotation, half, under.y - 0.1f, VoxelSpray.UNDER_HULL_DROPS, VoxelSpray.UNDER_HULL_BITS,
                VoxelSpray.UNDER_HULL_SPEED, _blood, _blood, VoxelSpray.UNDER_HULL_DROP_SIZE, VoxelSpray.UNDER_HULL_BIT_SIZE,
                _body.linearVelocity * INHERIT);
            TankBloodStains.Squash(point, 0.6f);
            SprayMarker.End();
        }

        /// <summary>Just above the ground under the contact, where the fans start; the contact itself if no ground is found.</summary>
        private static Vector3 Under(Vector3 contact)
        {
            if (Physics.Raycast(contact + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, GROUND_PROBE,
                    1 << PhysicsLayers.Ground, QueryTriggerInteraction.Ignore))
                return ground.point + Vector3.up * 0.1f;
            return contact;
        }

        /// <summary>
        /// A living enemy: the body a touched collider belongs to. One with a crush mark (the elite) only once its
        /// health is down to the mark (CrushRuleUseCase); the speed is checked by the callers.
        /// </summary>
        private static bool TryGetVictim(GameObject other, out IEntity victim)
        {
            if (!other.TryGetEntity(out victim) && !other.FindEntityInParent(out victim))
                return false;

            if (!victim.HasEnemyTag() || !victim.HasDamageableTag() ||
                !victim.TryGetHealth(out Health health) || health.IsEmpty())
                return false;

            float mark = victim.TryGetCrushBelowHealth(out IValue<float> crushBelow)
                ? crushBelow.Value
                : CrushRuleUseCase.NO_MARK;
            return CrushRuleUseCase.IsCrushable(health.GetPercent(), mark);
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
        /// <remarks>
        /// A splash costs about 0.15 ms (finding a spot of skin and laying voxels on it), so the
        /// <see cref="TankCrushConfig.splashes"/> of one crush, done at once, took 16–18 ms of the crush
        /// frame. They are queued instead and laid <see cref="STAINS_PER_STEP"/> a physics step
        /// (<see cref="StainStep"/>): the stain spreads over a fifth of a second. The burst is kept in the
        /// tank's own frame, so the blood lands where the body burst even as the tank drives on.
        /// </remarks>
        private void Stain(Vector3 burst, float groundY)
        {
            if (_skin == null || _skin.Bodies.Count == 0 || _blood.Length == 0 || _config.splashes <= 0)
                return;

            if (_pendingCount == PENDING_STAINS)
            {
                // A pile-up of crushes: the oldest stain gives way to the newest.
                System.Array.Copy(_pending, 1, _pending, 0, PENDING_STAINS - 1);
                _pendingCount--;
            }

            _pending[_pendingCount++] = new PendingStain
            {
                LocalBurst = _body.transform.InverseTransformPoint(burst),
                Height = burst.y - groundY,
                Next = 0
            };
        }

        /// <summary>Lays up to <see cref="STAINS_PER_STEP"/> queued splashes, oldest stain first.</summary>
        private void StainStep()
        {
            int budget = STAINS_PER_STEP;
            while (budget > 0 && _pendingCount > 0)
            {
                PendingStain stain = _pending[0];
                Vector3 burst = _body.transform.TransformPoint(stain.LocalBurst);
                int count = Mathf.Min(budget, _config.splashes - stain.Next);
                Splash(burst, burst.y - stain.Height, stain.Next, count);
                budget -= count;
                stain.Next += count;

                if (stain.Next < _config.splashes)
                {
                    _pending[0] = stain;
                    continue;
                }

                System.Array.Copy(_pending, 1, _pending, 0, _pendingCount - 1);
                _pendingCount--;
            }
        }

        /// <summary>Splashes <paramref name="first"/>..<paramref name="first"/>+<paramref name="count"/> of a burst.</summary>
        private void Splash(Vector3 burst, float groundY, int first, int count)
        {
            float reach = Mathf.Max(0.1f, _config.splashReach);
            Parts.Clear();
            Sizes.Clear();
            float total = 0f;
            // Indexed: a foreach over the IReadOnlyList would box its enumerator.
            IReadOnlyList<VoxelBody> bodies = _skin.Bodies;
            for (int b = 0; b < bodies.Count; b++)
            {
                VoxelBody part = bodies[b];
                if (part == null || !part.IsCreated || part.SolidCount == 0 ||
                    part.WorldBounds.SqrDistance(burst) > reach * reach)
                    continue;
                total += Mathf.Pow(part.SolidCount, 2f / 3f);
                Parts.Add(part);
                Sizes.Add(total);
            }
            if (Parts.Count == 0)
                return;

            for (int i = first; i < first + count; i++)
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
