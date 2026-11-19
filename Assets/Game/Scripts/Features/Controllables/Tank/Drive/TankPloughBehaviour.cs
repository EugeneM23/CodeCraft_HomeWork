using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using Unity.Profiling;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank's nose rips a chunk out of ground it hits at speed and the hull bounces off it, nose up,
    /// instead of stopping dead against it. The hull never goes into the ground: it collides with the
    /// terrain all the time and the springs are left alone.
    ///
    /// Every physics step it sweeps the hull's box a moment ahead along its flight. Ground the nose is
    /// about to hit hard enough (TankPloughMath.IsNoseHit) is dug out before it gets there — three digs
    /// across the nose, ahead and down into the slope, the notch the nose meets — and the earth dug out
    /// flies up and ahead as one big clump and crumbs (IEarthThrow, VoxelDebris.ThrowEarth). At the same
    /// moment the hull pushes off: it keeps part of its speed (SpeedLoss), its velocity is turned up the
    /// ramp of MaxRamp it rips out, the hop lifts the bow and the nose spins up (PitchKick).
    /// For ClimbTime after it the hull climbs no faster than that: the jump is the preset's, not the
    /// slope's. Out of a crater it jumps over the far rim nose-up rather than crawling out.
    ///
    /// A hit the sweep missed (the collider was rebuilt late, a thin ledge) shows as a step that
    /// lost much of the speed: it is answered after the fact, from the speed before it.
    ///
    /// The first plough of a session makes one empty dig above the ground at level load (WarmUp), so the
    /// first chunk does not pay for the carve job's first run mid-drive, and one throw of no earth, which
    /// JIT-compiles ThrowEarth and shows nothing.
    /// </summary>
    public sealed class TankPloughBehaviour : IEntityInit, IEntityFixedTick, IEntityDisable
    {
        private const int GROUND_MASK = 1 << PhysicsLayers.Ground;
        private const float SHRINK = 0.02f;
        private const float SKIN = 0.05f;
        private const float MISSED_LOSS = 0.4f;
        private const float MISSED_REACH = 0.4f;
        private const float BITE_ACROSS = 0.6f;
        // Voxels below a hit the chunk's colour is looked for.
        private const int COLOR_DEPTH = 24;
        // The chunk's colour when the ground has none to give where it was hit: plain earth.
        private static readonly Color32 EarthColor = new(110, 80, 52, 255);
        // How far (m) off the slope the chunk starts: clear of the notch's floor.
        private const float THROW_OFF = 0.3f;

        // How far (m) above the top of the ground's bounds the warm-up dig is made: nothing there to take.
        private const float WARM_UP_ABOVE = 10f;
        // Seconds of game time between looks for a ready terrain body to warm up on, while there is none.
        private const float WARM_UP_RETRY = 0.5f;

        private static readonly ProfilerMarker BiteMarker = new("TankPlough.Bite");
        private static readonly ProfilerMarker WarmUpMarker = new("TankPlough.WarmUp");
        private static readonly RaycastHit[] s_hits = new RaycastHit[8];
        // The first plough of the session has made its warm-up dig (WarmUp).
        private static bool s_warmedUp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_warmedUp = false;

        private readonly BoxCollider _hull;
        private readonly IEarthThrow _earth;
        private Rigidbody _body;
        private IValue<IEntity> _driver;

        private float _time;
        // When this tank next looks for a ready terrain body to warm up on, while none was.
        private float _nextWarmUpTry;
        private float _nextBite;
        private float _nextResponse;
        private Vector3 _lastVelocity;
        private bool _hasLast;
        // After a bounce: the most the hull may climb at (m/s), until when, and whether it has been off the
        // ground since (once it lands again the jump is over, and the cap with it).
        private float _climb;
        private float _climbUntil;
        private bool _climbAirborne;
        private TankSuspension _suspension;
        // Voxels the last chunk dug out: a hit that dug nothing bounces nothing.
        private int _lastDug;

        /// <summary>The preset in use.</summary>
        public TankPloughConfig Config { get; set; }

        /// <summary>Hits the hull bounced off. For tests.</summary>
        public int Impacts { get; private set; }

        /// <summary>Debris cubes the chunks threw (VoxelDebris.ThrowEarth). For tests.</summary>
        public int ThrownCubes { get; private set; }

        /// <summary>Chunks thrown (ThrowEarth calls). For tests.</summary>
        public int Throws { get; private set; }

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

        /// <summary>A dead or disabled tank comes back with no climb cap left over.</summary>
        public void Disable(IEntity entity) => _climbUntil = 0f;

        public void FixedTick(IEntity entity, float deltaTime)
        {
            _time += deltaTime;
            if (!s_warmedUp && _time >= _nextWarmUpTry)
            {
                _nextWarmUpTry = _time + WARM_UP_RETRY;
                WarmUp();
            }
            TankPloughConfig config = Config;
            if (config == null || _hull == null || _body.isKinematic)
            {
                _hasLast = false;
                _climbUntil = 0f;
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
            if (_time < _climbUntil)
                CapClimb();

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
                // Already overlapping at the start of the cast: Unity gives no point or normal to go by,
                // so it is no hit; the missed-hit check covers it.
                if (hit.distance <= 0f && hit.point == Vector3.zero)
                    continue;
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

            // Brushing a rut's wall or a crater's side is no nose hit: the face must stand across the way the hull goes.
            if (!TankPloughMath.FacesTravel(normal, velocity))
                return;

            VoxelBody ground = FindGround(hit.point);
            if (ground == null)
                return;

            // The nose rips a ramp of MaxRamp out of what it hit, straight ahead: the hull pushes off along that,
            // not along whichever facet of the voxel staircase the box met first.
            Vector3 ramp = TankPloughMath.RampAhead(velocity, config.MaxRamp);
            float strength = TankPloughMath.Strength(-Vector3.Dot(velocity, ramp), config.MinClosing, config.FullClosing);
            // Into the face hard enough, but glancing off the ramp it would cut (already pushing off up it):
            // no hit at all, not even a chunk.
            if (strength <= 0f)
                return;

            if (_time >= _nextBite)
            {
                _nextBite = _time + config.BiteInterval;
                _lastDug = RipChunk(ground, hit.point, normal, velocity, ramp, strength, config);
            }

            // Nothing ripped out (bedrock, a sliver of ground): nothing to push off, no bounce, no sound.
            if (_lastDug <= 0 || _time < _nextResponse)
                return;
            _nextResponse = _time + config.ImpactCooldown;
            Impacts++;
            Bounce(velocity, ramp, strength, config);

            // How hard it felt (sound, shake): against a slope of IMPACT_RAMP, on the scale all
            // presets share. The response strength above is against the ramp the preset cuts, so it is no
            // measure of the hit. A hit at IMPACT_MIN_CLOSING or below bounces the hull but makes no impact:
            // no sound, no shake.
            float impact = TankPloughMath.ImpactStrength(TankPloughMath.ImpactClosing(velocity), config.ImpactScale);
            if (impact <= 0f)
                return;
            if (_driver != null && _driver.Value != null && config.ShakeStrength > 0f)
                CameraShakeUseCase.ShakePlayerCamera(new CameraShakeArgs(config.ShakeDuration, config.ShakeStrength * impact)); // perf-ok: once per impact (at most one per ImpactCooldown), not per step
            TankPloughEvents.RaisePloughed(hit.point, impact);
        }

        /// <summary>
        /// The hull pushes off the spot it hit, at once: part of the speed kept, turned up the ramp,
        /// the bow lifted and the nose spinning up.
        /// </summary>
        private void Bounce(Vector3 velocity, Vector3 ramp, float strength, TankPloughConfig config)
        {
            Transform hull = _hull.transform;
            _body.linearVelocity = TankPloughMath.Deflect(velocity, ramp, strength, config.SpeedLoss, config.Deflect);
            // The nose comes up: at least this pitch rate, whatever the hull was spinning before.
            _body.angularVelocity = TankPloughMath.NoseUpSpin(_body.angularVelocity, hull.right, config.PitchKick * strength);
            // The hop lifts the bow, not the middle: Hop · strength m/s up, and the nose comes up with it.
            Vector3 bow = hull.TransformPoint(_hull.center + new Vector3(0f, -_hull.size.y * 0.5f, _hull.size.z * 0.5f));
            _body.AddForceAtPosition(Vector3.up * (config.Hop * strength * _body.mass), bow, ForceMode.Impulse);
            // The jump is the bounce's: riding up the rest of a steeper slope, the hull would be thrown
            // off its rim as off a ski jump, as high as the slope happens to be steep there.
            // Never below the hop itself: a steep, slow landing nose down would turn the cap into a push down.
            // A hit while a cap is on only raises it; it does not make it last longer.
            float climb = Mathf.Max(_body.linearVelocity.y + config.Hop * strength, config.Hop * strength);
            if (_time < _climbUntil)
                _climb = Mathf.Max(_climb, climb);
            else
            {
                _climb = climb;
                _climbUntil = _time + config.ClimbTime;
                _climbAirborne = false;
            }
        }

        /// <summary>
        /// The hull climbs no faster than the bounce sent it, until it has been off the ground and landed again
        /// (or ClimbTime is up). Only the climb is cut: the ground still stops it, so where the slope is
        /// steeper it gives up speed along it rather than going up faster.
        /// </summary>
        private void CapClimb()
        {
            if (_suspension != null)
            {
                if (_suspension.GroundedCount == 0)
                    _climbAirborne = true;
                else if (_climbAirborne)
                {
                    _climbUntil = 0f;
                    return;
                }
            }

            Vector3 velocity = _body.linearVelocity;
            if (velocity.y > _climb)
                _body.linearVelocity = new Vector3(velocity.x, _climb, velocity.z);
        }

        /// <summary>
        /// The nose rips a chunk out where it hit: three digs across it, ahead and down into the slope
        /// (the notch is there before the hull is), and the earth they took flies up and ahead.
        /// </summary>
        /// <returns>Voxels dug out.</returns>
        private int RipChunk(VoxelBody ground, Vector3 point, Vector3 hitNormal, Vector3 velocity, Vector3 normal, float strength,
            TankPloughConfig config)
        {
            using (BiteMarker.Auto())
            {
                // Across the whole nose, wherever along it the ground was met: the middle dig on the hull's centre line.
                Transform hull = _hull.transform;
                point -= hull.right * Vector3.Dot(point - hull.TransformPoint(_hull.center), hull.right);
                float radius = Mathf.Lerp(config.BiteRadiusMin, config.BiteRadiusMax, strength);
                Vector3 heading = velocity.normalized;
                Vector3 centre = point + heading * (radius * 0.4f) - normal * (radius * config.BiteDepth);
                Vector3 across = hull.right * (Vector3.Scale(_hull.size, hull.lossyScale).x * 0.5f * BITE_ACROSS);

                // The colours before the digs take them: turf on top, the earth under it at the notch's floor.
                float voxel = ground.VoxelSize * Mathf.Abs(ground.transform.lossyScale.x);
                Color32 top = ColorAt(ground, point, hitNormal, voxel, EarthColor);
                Color32 body = ColorAt(ground, centre, Vector3.zero, voxel, top);

                int dug = _earth.Dig(ground, centre, radius) +
                          _earth.Dig(ground, centre + across, radius * 0.85f) +
                          _earth.Dig(ground, centre - across, radius * 0.85f);

                float volume = TankPloughMath.ChunkVolume(dug, voxel, config.ThrowVolume);
                if (volume <= 0f)
                    return dug;
                var earth = new EarthThrow
                {
                    Point = point + normal * THROW_OFF,
                    Velocity = TankPloughMath.ChunkThrow(velocity, config.ThrowLift, config.ThrowScale),
                    Volume = volume,
                    Spread = Mathf.Lerp(config.SpreadMin, config.SpreadMax, strength),
                    Top = top,
                    Body = body
                };
                ThrownCubes += _earth.Throw(earth);
                Throws++;
                return dug;
            }
        }

        /// <summary>
        /// The colour of the ground at <paramref name="point"/>: half a voxel into the face it lies on
        /// (<paramref name="normal"/>), else the first solid voxel below it within COLOR_DEPTH voxels (a riser's
        /// face has air under it down to the tread below), else <paramref name="fallback"/>.
        /// </summary>
        private static Color32 ColorAt(VoxelBody ground, Vector3 point, Vector3 normal, float voxel, Color32 fallback)
        {
            if (normal != Vector3.zero && ground.TryGetColor(point - normal * (voxel * 0.5f), out Color32 color))
                return color;
            // TryGetColor looks 4 voxels down from where it is asked.
            for (int i = 0; i * 4 < COLOR_DEPTH; i++)
                if (ground.TryGetColor(point + Vector3.up * (voxel * (1 - 4 * i)), out color))
                    return color;
            return fallback;
        }

        /// <summary>
        /// Once a session, at the first physics step a terrain body is ready (level load): one dig through
        /// IEarthThrow well above the ground, where there is nothing to take. It removes nothing and rebuilds
        /// nothing, but the carve job runs for the first time here and not in the first chunk mid-drive: its
        /// first schedule (job reflection data and, in the editor, Burst's options and compile) and the dig
        /// code's JIT. Then a throw of no earth: ThrowEarth returns at once and throws nothing, but its own
        /// code is JIT-compiled here (not what it calls, which only a real throw reaches).
        /// </summary>
        private void WarmUp()
        {
            var bodies = VoxelWorld.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody body = bodies[i];
                if (body == null || !body.IsTerrain || !body.IsCreated || body.SolidCount == 0)
                    continue;

                s_warmedUp = true;
                Bounds bounds = body.WorldBounds;
                var above = new Vector3(bounds.center.x, bounds.max.y + WARM_UP_ABOVE, bounds.center.z);
                using (WarmUpMarker.Auto())
                {
                    _earth.Dig(body, above, Config != null ? Config.BiteRadiusMax : 1f);
                    _earth.Throw(new EarthThrow { Point = above });
                }

                return;
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
