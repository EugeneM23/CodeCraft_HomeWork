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
    /// — three bites across the nose, thrown forward and up — and the hull is given the velocity it
    /// leaves the hit with: a share of its speed gone, turned up along the slope, nose up. Out of a
    /// crater it comes over the far rim in a short jump rather than crawling out.
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
        // Steepest slope (°) the hull leaves a hit along: a voxel step's vertical face is dug out, not climbed.
        private const float MAX_RAMP = 30f;

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
        private CameraShakeArgs _shake;

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
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            _time += deltaTime;
            TankPloughConfig config = Config;
            if (config == null || _hull == null || _body.isKinematic)
                return;

            Vector3 velocity = _body.linearVelocity;
            float speed = velocity.magnitude;

            // Missed: the solver stopped the hull against ground the sweep did not see.
            if (_hasLast && _lastVelocity.magnitude >= config.MinSpeed && speed < _lastVelocity.magnitude * (1f - MISSED_LOSS) &&
                Sweep(_lastVelocity, MISSED_REACH, out RaycastHit missed))
                Hit(missed, _lastVelocity, config);
            else if (speed >= config.MinSpeed && Sweep(velocity, speed * config.LookAhead + SKIN, out RaycastHit ahead))
                Hit(ahead, velocity, config);

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
            Vector3 ramp = TankPloughMath.Ramp(normal, MAX_RAMP);
            float strength = TankPloughMath.Strength(-Vector3.Dot(velocity, ramp), config.MinClosing, config.FullClosing);

            if (_time >= _nextBite)
            {
                _nextBite = _time + config.BiteInterval;
                Bite(ground, hit.point, velocity, ramp, strength, config);
            }

            if (_time < _nextResponse)
                return;

            _nextResponse = _time + config.ImpactCooldown;
            Impacts++;
            _body.linearVelocity = TankPloughMath.Deflect(velocity, ramp, strength, config.SpeedLoss, config.Deflect,
                config.Hop);
            _body.angularVelocity += -hull.right * (config.PitchKick * strength); // negative about right = nose up

            if (config.DustRadius > 0f)
                VoxelSmoke.Blast(hit.point, config.DustRadius * strength, 0.6f);
            if (_driver != null && _driver.Value != null && config.ShakeStrength > 0f)
                CameraShakeUseCase.ShakePlayerCamera(new CameraShakeArgs(config.ShakeDuration, config.ShakeStrength * strength));
            TankPloughEvents.RaisePloughed(hit.point, strength);
        }

        private void Bite(VoxelBody ground, Vector3 point, Vector3 velocity, Vector3 normal, float strength,
            TankPloughConfig config)
        {
            using (BiteMarker.Auto())
            {
                float radius = Mathf.Lerp(config.BiteRadiusMin, config.BiteRadiusMax, strength);
                Vector3 heading = velocity.normalized;
                Vector3 centre = point + heading * (radius * 0.4f);
                Vector3 across = _hull.transform.right * (Vector3.Scale(_hull.size, _hull.transform.lossyScale).x * 0.5f * BITE_ACROSS);
                Vector3 thrown = TankPloughMath.Throw(velocity, normal, config.ThrowLift, config.ThrowScale);
                float spread = Mathf.Lerp(config.SpreadMin, config.SpreadMax, strength);

                _earth.Excavate(ground, centre, radius, thrown, spread, config.ThrowVolume);
                _earth.Excavate(ground, centre + across, radius * 0.85f, thrown, spread, config.ThrowVolume);
                _earth.Excavate(ground, centre - across, radius * 0.85f, thrown, spread, config.ThrowVolume);
            }
        }

        private static VoxelBody FindGround(Vector3 point)
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
