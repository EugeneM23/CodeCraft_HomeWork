using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using Unity.Profiling;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank's nose ploughs into ground it hits at speed instead of stopping dead against it.
    ///
    /// Every physics step it sweeps the hull's box a moment ahead along its flight. Ground the
    /// nose is about to hit hard enough (TankPloughMath.IsNoseHit) is dug out before it gets there
    /// — three bites across the nose, deep and thrown forward and up — and the hit costs a share of
    /// the speed. Then the nose digs in for PloughTime: it keeps cutting under and ahead of itself,
    /// the hull holds its heading into the slope on soft springs and goes through ground whose
    /// collider is not rebuilt yet. Then it rises: turned up the ramp it cut (no steeper than
    /// MaxRamp), the nose coming up to RisePitch, its climb capped while it is still in the ground.
    /// Out of a crater it comes over the far rim nose-up in a short hop rather than crawling out.
    ///
    /// A hit the sweep missed (the collider was rebuilt late, a thin ledge) shows as a step that
    /// lost much of the speed: it is answered after the fact, from the speed before it.
    /// </summary>
    public sealed class TankPloughBehaviour : IEntityInit, IEntityFixedTick
    {
        private const int GROUND_MASK = 1 << 3;
        private const float SHRINK = 0.02f;
        private const float SKIN = 0.05f;
        private const float MISSED_LOSS = 0.4f;
        private const float MISSED_REACH = 0.4f;
        private const float BITE_ACROSS = 0.6f;
        // Seconds the springs take to come back to full stiffness after the hull rose: all at once, pressed
        // deep into the cut, they would throw it high.
        private const float SPRING_RETURN = 0.4f;
        // Longest (s) the hull keeps going through the ground after it rose, while it is still in it.
        private const float MAX_PASS = 0.5f;

        private static readonly ProfilerMarker BiteMarker = new("TankPlough.Bite");
        private static readonly RaycastHit[] s_hits = new RaycastHit[8];

        private readonly BoxCollider _hull;
        private readonly IEarthThrow _earth;
        private Rigidbody _body;
        private IValue<IEntity> _driver;

        private float _time;
        private float _nextBite;
        private float _nextResponse;
        private Vector3 _lastVelocity;
        private bool _hasLast;
        // While ploughing: the ground being cut and how hard, until when.
        private VoxelBody _ploughGround;
        private float _ploughStrength;
        private float _ploughUntil;
        // After a hit: when the hull comes up out of the cut, along what and how hard.
        private bool _rising;
        private float _riseAt;
        private Vector3 _riseRamp;
        private float _riseStrength;
        // The ground layer was already excluded from the body's contacts before the plough added it.
        private bool _groundWasExcluded;
        private bool _passing;
        private float _passUntil;
        // The heading the hull digs in along, and the most it may climb at (m/s) once it rose while still in the ground.
        private Vector3 _digHeading;
        private float _riseClimb;
        private float _capUntil;
        // The springs, softened while the nose digs in, and what they were.
        private TankSuspension _suspension;
        private float _stiffness;
        private float _damping;
        private bool _soft;
        private float _springShare = 1f;

        public TankPloughConfig Config { get; set; }

        /// <summary>Hits the hull was turned by. For tests.</summary>
        public int Impacts { get; private set; }

        public TankPloughBehaviour(BoxCollider hull, TankPloughConfig config, IEarthThrow earth)
        {
            _hull = hull;
            Config = config;
            _earth = earth;
        }

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _driver = entity.TryGetDriver(out IReactiveVariable<IEntity> driver) ? driver : null;
            _suspension = entity.TryGetSuspension(out TankSuspension suspension) ? suspension : null;
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            _time += deltaTime;
            TankPloughConfig config = Config;
            if (config == null || _hull == null || _body.isKinematic)
            {
                _rising = false;
                PassGround(false); // never left going through the ground
                Springs(1f);

                return;
            }

            Vector3 velocity = _body.linearVelocity;
            float speed = velocity.magnitude;

            // Missed: the solver stopped the hull against ground the sweep did not see.
            if (_hasLast && _lastVelocity.magnitude >= config.MinSpeed && speed < _lastVelocity.magnitude * (1f - MISSED_LOSS) &&
                Sweep(_lastVelocity, MISSED_REACH, out RaycastHit missed))
                Hit(missed, _lastVelocity, config);
            else if (speed >= config.MinSpeed && Sweep(velocity, speed * config.LookAhead + SKIN, out RaycastHit ahead))
                Hit(ahead, velocity, config);

            // Ploughing on: the nose keeps cutting under and ahead of itself, so the hull sinks into the cut.
            if (_ploughGround != null && _time < _ploughUntil && _time >= _nextBite && speed >= config.MinSpeed)
                PloughOn(velocity, config);
            if (_rising && _time >= _riseAt)
                Rise(config);
            if (_rising)
                Hold();
            else
            {
                if (_soft)
                    Springs(Mathf.MoveTowards(_springShare, 1f, deltaTime / SPRING_RETURN));
                if (_passing && (_time >= _passUntil || !InGround()))
                    PassGround(false);
                if (_time < _capUntil)
                    CapClimb(config);
            }

            _lastVelocity = _body.linearVelocity;
            _hasLast = true;
        }

        /// <summary>The nearest ground the hull's box meets along <paramref name="velocity"/> within <paramref name="reach"/> m.</summary>
        private bool Sweep(Vector3 velocity, float reach, out RaycastHit nearest)
        {
            nearest = default;
            float speed = velocity.magnitude;
            if (speed < 1e-3f)
                return false;

            Transform hull = _hull.transform;
            Vector3 centre = hull.TransformPoint(_hull.center);
            Vector3 half = Vector3.Scale(_hull.size, hull.lossyScale) * 0.5f - Vector3.one * SHRINK;
            int count = Physics.BoxCastNonAlloc(centre, half, velocity / speed, s_hits, hull.rotation, reach, GROUND_MASK,
                QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hits[i];
                if (hit.distance <= 0f && hit.point == Vector3.zero)
                    continue; // already overlapping: no normal to go by
                if (hit.distance < best)
                {
                    best = hit.distance;
                    nearest = hit;
                }
            }

            return best < float.MaxValue;
        }

        private void Hit(RaycastHit hit, Vector3 velocity, TankPloughConfig config)
        {
            Transform hull = _hull.transform;
            Vector3 normal = hit.normal;
            float closing = -Vector3.Dot(velocity, normal);
            Vector3 local = hull.InverseTransformPoint(hit.point) - _hull.center;
            if (!TankPloughMath.IsNoseHit(local, normal, hull.forward, closing, config.MinClosing))
                return;

            VoxelBody ground = FindGround(hit.point);
            if (ground == null)
                return;

            // The nose cuts a ramp through what it hit: the hull goes on along that, not along a step's face.
            Vector3 ramp = TankPloughMath.Ramp(normal, config.MaxRamp);
            float strength = TankPloughMath.Strength(-Vector3.Dot(velocity, ramp), config.MinClosing, config.FullClosing);
            _ploughGround = ground;
            _ploughStrength = strength;
            _ploughUntil = _time + config.PloughTime;

            if (_time >= _nextBite)
            {
                _nextBite = _time + config.BiteInterval;
                Bite(ground, hit.point, velocity, ramp, strength, config);
            }

            if (_time < _nextResponse)
                return;

            // No new dig until the hull has climbed out of this one: at the rim it would burrow on, not come up.
            _nextResponse = _time + config.PloughTime + Mathf.Max(config.ImpactCooldown, MAX_PASS);
            Impacts++;
            // The hit costs its speed now, but the hull keeps its heading into the cut while the nose
            // ploughs on (PloughTime); only then does it come up the ramp (Rise).
            float speed = velocity.magnitude;
            _body.linearVelocity = velocity * (TankPloughMath.SpeedAfter(speed, strength, config.SpeedLoss) / Mathf.Max(speed, 1e-3f));
            _riseRamp = ramp;
            _riseStrength = strength;
            _digHeading = velocity.normalized;
            _riseAt = _time + config.PloughTime;
            PassGround(true);
            _rising = true;
            Springs(Mathf.Min(_soft ? _springShare : 1f, config.DigSpring));

            if (config.DustRadius > 0f)
                VoxelSmoke.Blast(hit.point, config.DustRadius * strength, 0.6f);
            if (_driver != null && _driver.Value != null && config.ShakeStrength > 0f)
                CameraShakeUseCase.ShakePlayerCamera(new CameraShakeArgs(config.ShakeDuration, config.ShakeStrength * strength));
            TankPloughEvents.RaisePloughed(hit.point, strength);
        }

        /// <summary>The nose has dug in: the hull comes up the ramp it cut, nose up.</summary>
        private void Rise(TankPloughConfig config)
        {
            _rising = false;
            _passUntil = _time + MAX_PASS;
            _capUntil = _time + MAX_PASS;
            Transform hull = _hull.transform;
            float strength = _riseStrength;
            _body.linearVelocity = TankPloughMath.Deflect(_body.linearVelocity, _riseRamp, strength, 0f, config.Deflect, 0f);
            // The nose comes up: at least this pitch rate, whatever the hull was spinning before.
            _body.angularVelocity = TankPloughMath.NoseUpSpin(_body.angularVelocity, hull.right, config.PitchKick * strength);
            // The hop lifts the bow, not the middle: Hop · strength m/s up, and the nose comes up with it.
            Vector3 bow = hull.TransformPoint(_hull.center + new Vector3(0f, -_hull.size.y * 0.5f, _hull.size.z * 0.5f));
            _body.AddForceAtPosition(Vector3.up * (config.Hop * strength * _body.mass), bow, ForceMode.Impulse);
            _riseClimb = _body.linearVelocity.y + config.Hop * strength;
        }

        /// <summary>
        /// While the nose digs in the hull goes on into the slope, not up it: the springs, pressed
        /// against ground not yet cut, would throw it up and nose-up before the cut is made.
        /// </summary>
        private void Hold()
        {
            float speed = Mathf.Max(0f, Vector3.Dot(_body.linearVelocity, _digHeading));
            _body.linearVelocity = _digHeading * speed;
            _body.angularVelocity = TankPloughMath.NoNoseUpSpin(_body.angularVelocity, _hull.transform.right);
        }

        /// <summary>
        /// Risen but still in the ground: the springs, pressed hard into the cut, would throw the hull
        /// high; it climbs no faster than the rise sent it, and its nose comes up to RisePitch.
        /// </summary>
        private void CapClimb(TankPloughConfig config)
        {
            // The nose keeps coming up until it is at RisePitch.
            Transform hull = _hull.transform;
            if (Mathf.Asin(Mathf.Clamp(hull.forward.y, -1f, 1f)) * Mathf.Rad2Deg < config.RisePitch)
                _body.angularVelocity = TankPloughMath.NoseUpSpin(_body.angularVelocity, hull.right, config.PitchKick * _riseStrength);
            Vector3 velocity = _body.linearVelocity;
            if (velocity.y > _riseClimb)
                _body.linearVelocity = new Vector3(velocity.x, _riseClimb, velocity.z);
        }

        /// <summary>
        /// The springs at <paramref name="share"/> of their stiffness: soft while the nose digs in, so the hull
        /// sinks into the cut; at 1 back exactly as they were.
        /// </summary>
        private void Springs(float share)
        {
            if (_suspension == null)
                return;
            if (!_soft)
            {
                if (share >= 1f)
                    return;
                _stiffness = _suspension.Stiffness;
                _damping = _suspension.Damping;
                _soft = true;
            }

            _springShare = Mathf.Min(share, 1f);
            _suspension.Stiffness = _stiffness * _springShare;
            _suspension.Damping = _damping * _springShare;
            if (share >= 1f)
                _soft = false;
        }

        /// <summary>The hull's box is still in the ground.</summary>
        private bool InGround()
        {
            Transform hull = _hull.transform;
            Vector3 half = Vector3.Scale(_hull.size, hull.lossyScale) * 0.5f;
            return Physics.CheckBox(hull.TransformPoint(_hull.center), half, hull.rotation, GROUND_MASK,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// While the nose ploughs the hull's colliders go through the ground: the cut's collider is
        /// rebuilt a few frames after each bite, too late for a hull at speed, which would otherwise
        /// stop dead against the slope it is cutting. The springs (sphere casts) still carry it.
        /// </summary>
        private void PassGround(bool pass)
        {
            if (pass == _passing)
                return;
            _passing = pass;
            LayerMask exclude = _body.excludeLayers;
            if (pass)
            {
                _groundWasExcluded = (exclude.value & GROUND_MASK) != 0;
                _body.excludeLayers = exclude.value | GROUND_MASK;
            }
            else if (!_groundWasExcluded)
                _body.excludeLayers = exclude.value & ~GROUND_MASK;
        }

        /// <summary>A bite under and ahead of the bow while ploughing on after a nose hit.</summary>
        private void PloughOn(Vector3 velocity, TankPloughConfig config)
        {
            _nextBite = _time + config.BiteInterval;
            Vector3 bow = _hull.transform.TransformPoint(_hull.center + new Vector3(0f, -_hull.size.y * 0.5f, _hull.size.z * 0.5f));
            // As far ahead as the hull goes before the ground's collider is rebuilt.
            Bite(_ploughGround, bow + velocity * config.LookAhead, velocity, Vector3.up, _ploughStrength, config);
        }

        private void Bite(VoxelBody ground, Vector3 point, Vector3 velocity, Vector3 normal, float strength,
            TankPloughConfig config)
        {
            using (BiteMarker.Auto())
            {
                // Across the whole nose, wherever along it the ground was met: the middle bite on the hull's centre line.
                Transform hull = _hull.transform;
                point -= hull.right * Vector3.Dot(point - hull.TransformPoint(_hull.center), hull.right);
                float radius = Mathf.Lerp(config.BiteRadiusMin, config.BiteRadiusMax, strength);
                Vector3 heading = velocity.normalized;
                // Ahead and down into the slope: the cut the nose sinks into before the hull is turned.
                Vector3 centre = point + heading * (radius * 0.4f) - normal * (radius * config.BiteDepth);
                Vector3 across = hull.right * (Vector3.Scale(_hull.size, hull.lossyScale).x * 0.5f * BITE_ACROSS);
                Vector3 thrown = TankPloughMath.Throw(velocity, normal, config.ThrowLift, config.ThrowScale);
                float spread = Mathf.Lerp(config.SpreadMin, config.SpreadMax, strength);

                _earth.Excavate(ground, centre, radius, thrown, spread, config.ThrowVolume, config.BiteDebris);
                _earth.Excavate(ground, centre + across, radius * 0.85f, thrown, spread, config.ThrowVolume, config.BiteDebris);
                _earth.Excavate(ground, centre - across, radius * 0.85f, thrown, spread, config.ThrowVolume, config.BiteDebris);
            }
        }

        /// <summary>The terrain body whose bounds hold <paramref name="point"/>, or null.</summary>
        public static VoxelBody FindGround(Vector3 point)
        {
            var bodies = VoxelWorld.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody body = bodies[i];
                if (body != null && body.IsTerrain && body.IsCreated && body.WorldBounds.SqrDistance(point) < 0.01f)
                    return body;
            }

            return null;
        }
    }
}
