using System;
using Atomic.Entities;
using Game.Horde;
using Game.Voxels;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Horde monsters that leap onto the player's tank and ride it (the user: «зомби запрыгивают на танк»). A monster
    /// of a leaping kind (<see cref="HordeRiderOptions.kindMask"/>) within <see cref="HordeRidingSettings.LeapRange"/> of
    /// the hull may leap, mostly while the tank is slow, onto a free attach point (<see cref="HordeAttachPoint"/>: two on
    /// the front deck, the fenders, the rear deck, the turret roof; one rider each). It flies an arc to the point, clings,
    /// then tears at the armour (strikes into the tank's health through the horde's damage). Its grip wears down with
    /// sharp turns, speed and jolts (a ram, a hard stop) and with hits (the tank's own guns, blasts); at zero it is thrown
    /// off, falls and walks again. Killed on the tank, it dies the horde's way (corpse by the hull, goo from where it held on).
    /// <para>
    /// The slot stays in the simulation as <see cref="HordeState.Riding"/>: combat targets it as usual. This class places it
    /// every frame between the step's Complete and Schedule (<see cref="Tick"/>) and draws it itself at this frame's pose
    /// of the tank. Events and the animation hook: <see cref="HordeRiderEvents"/>. At most <see cref="MAX_RIDERS"/> riders,
    /// a scan of the crowd ten times a second, no managed allocations per frame.
    /// </para>
    /// </summary>
    public sealed class HordeRiders : IDisposable
    {
        public const int MAX_RIDERS = 6;
        private const float SCAN_INTERVAL = 0.1f;
        private const int FALLEN_RING = 16;
        /// <summary>A fall ends after this long even with no ground found (off the grid).</summary>
        private const float MAX_FALL = 3f;
        /// <summary>The corpse of one killed on the tank lies this far out from the hull's side.</summary>
        private const float DROP_OUT = 0.7f;

        private static readonly ProfilerMarker TickMarker = new("Horde.Riders");

        private readonly HordeRiderOptions _options;
        private readonly Rider[] _riders = new Rider[MAX_RIDERS];
        private readonly Point[] _points = new Point[MAX_RIDERS];
        private readonly int2[] _fallen = new int2[FALLEN_RING];
        private readonly float[] _fallenTime = new float[FALLEN_RING];
        private readonly HordeRenderer[] _renderers;
        private readonly int[] _drawn;
        private NativeArray<HordeInstance> _instances;
        private Color32[] _goo;
        private float _gooSize;

        private int _count;
        private int _fallenHead;
        private Transform _tank;
        private Rigidbody _body;
        private Transform _turret;
        private Vector3 _hullCenter;
        private Vector3 _hullHalf;
        private bool _bound;
        private Vector3 _lastVelocity;
        private float _lastTurretYaw;
        private bool _hasLast;
        private float _scanTimer;
        private float _leapTimer;
        private float _damage;
        private Unity.Mathematics.Random _random = new(0xB0A2D1u);
        private HordeClip _run;
        private HordeClip _attack;
        private HordeClip _idle;

        private struct Rider
        {
            public int Slot;
            public int Version;
            public byte Kind;
            public HordeRiderState State;
            public HordeAttachPoint Point;
            public float StateTime;
            public float Duration;
            public float Grip;
            public float LastHealth;
            public float StrikeTimer;
            public int Strikes;
            public float AnimTime;
            public float3 From;
            /// <summary>Height of the ground it leapt from: where it lands when the grid has none under it.</summary>
            public float GroundY;
            public float3 Feet;
            public float3 Velocity;
            public float Heading;
            public HordeShakeCause Cause;
        }

        /// <summary>An attach point: feet and yaw in the hull's (or the turret's) space.</summary>
        private struct Point
        {
            public Vector3 Local;
            public float Yaw;
            public bool OnTurret;
            public bool Taken;
        }

        public HordeRiders(HordeRiderOptions options, HordeArtConfig art, HordeOptions horde, float bodyScale)
        {
            _options = options ?? new HordeRiderOptions();
            _renderers = new HordeRenderer[HordeKinds.COUNT];
            _drawn = new int[HordeKinds.COUNT];
            for (int k = 0; k < FALLEN_RING; k++)
                _fallen[k] = new int2(-1, -1); // empty: no slot fell yet
            _instances = new NativeArray<HordeInstance>(MAX_RIDERS * HordeKinds.COUNT, Allocator.Persistent);
            if (art != null && art.mesh != null && art.material != null)
            {
                for (int row = 0; row < HordeKinds.COUNT; row++)
                {
                    _renderers[row] = new HordeRenderer(art.mesh, art.material, MAX_RIDERS);
                    _renderers[row].SetLook(horde.Kind(row).RenderScale, art.TintOf(row), art.EmissionOf(row));
                }

                _run = art.run;
                _attack = art.attack;
                _idle = art.idle;
            }

            var look = new BloodLook();
            Color32[] blood = look.ToVoxels();
            _goo = new Color32[] { blood[0], blood[blood.Length - 1], new Color(0.55f, 0.12f, 0.12f) };
            _gooSize = look.dropSize * 3f;
            BodyScale = bodyScale;
        }

        /// <summary>Riders on the tank now (leaping and falling ones included).</summary>
        public int Count => _count;

        /// <summary>Model height over the Regax's (the horde's body scale).</summary>
        public float BodyScale { get; set; }

        /// <summary>Copy of rider <paramref name="n"/> (0..<see cref="Count"/>-1).</summary>
        public HordeRiderInfo GetRider(int n) => Info(_riders[n], _riders[n].State);

        /// <summary>The rider of horde slot <paramref name="slot"/>, if it rides.</summary>
        public bool TryGetRiderOf(int slot, out HordeRiderInfo info)
        {
            for (int n = 0; n < _count; n++)
            {
                if (_riders[n].Slot != slot)
                    continue;
                info = GetRider(n);
                return true;
            }

            info = default;
            return false;
        }

        /// <summary>
        /// The player's tank (or none): its root, body, turret and hull box (in the root's space). Riders of an old tank are
        /// shaken off. Measures the attach points against the tank's colliders (an event, may allocate).
        /// </summary>
        public void Bind(IEntity tank, Transform root, Rigidbody body, Vector3 hullCenter, Vector3 hullHalf)
        {
            for (int n = 0; n < _count; n++)
                if (_riders[n].State != HordeRiderState.ShakenOff)
                    ShakeOff(ref _riders[n], float3.zero, HordeShakeCause.TankLost);
            for (int p = 0; p < _points.Length; p++)
                _points[p].Taken = false;

            _tank = root;
            _body = body;
            _turret = null;
            _hullCenter = hullCenter;
            _hullHalf = hullHalf;
            _bound = root != null;
            _hasLast = false;
            if (!_bound)
                return;
            if (tank != null && tank.TryGetTurretVisual(out Transform turret))
                _turret = turret;
            MeasurePoints();
        }

        /// <summary>
        /// The frame: deaths of riders (their corpses moved off the tank, before the horde hands the deaths out), the
        /// riders' states, new leaps; every rider placed in <paramref name="simulation"/>. Call between Complete and Schedule.
        /// </summary>
        public void Tick(HordeSimulation simulation, HordeNavGrid grid, float deltaTime)
        {
            if (simulation == null || simulation.IsRunning)
                return;
            using (TickMarker.Auto())
            {
                HordeRidingSettings s = _options.Settings;
                float dt = math.max(deltaTime, 0f);
                ReadMotion(dt, out float3 tankVelocity, out float yawRate, out float turretRate, out float jolt);
                float speed = math.length(tankVelocity.xz);
                float hullShake = HordeRiding.Shake(s, yawRate, speed, jolt);
                float turretShake = HordeRiding.Shake(s, math.max(math.abs(yawRate), math.abs(turretRate)), speed, jolt);

                int n = 0;
                while (n < _count)
                {
                    ref Rider r = ref _riders[n];
                    if (!simulation.IsTargetAlive(r.Slot, r.Version) || simulation.GetState(r.Slot) != HordeState.Riding)
                    {
                        RiderDied(simulation, grid, r);
                        Remove(n);
                        continue;
                    }

                    if (!_bound && r.State != HordeRiderState.ShakenOff)
                        ShakeOff(ref r, float3.zero, HordeShakeCause.TankLost);

                    float health = simulation.GetHealth(r.Slot);
                    int hits = health < r.LastHealth - 1e-3f ? 1 : 0;
                    r.LastHealth = health;
                    r.StateTime += dt;
                    r.AnimTime += dt;

                    if (r.State == HordeRiderState.ShakenOff)
                    {
                        if (Fall(simulation, grid, ref r, dt))
                        {
                            Remove(n);
                            continue;
                        }

                        n++;
                        continue;
                    }

                    PointPose(r.Point, out float3 at, out float yaw);
                    bool turret = _points[(int)r.Point].OnTurret;
                    float shake = r.State == HordeRiderState.Leaping ? 0f : turret ? turretShake : hullShake;
                    r.Grip = HordeRiding.Grip(s, r.Grip, shake, hits, dt);
                    if (r.Grip <= 0f)
                    {
                        ShakeOff(ref r, PointVelocity(at, tankVelocity), hits > 0 ? HordeShakeCause.Hit : HordeShakeCause.Motion);
                        simulation.SetRidePose(r.Slot, r.Feet, r.Heading);
                        n++;
                        continue;
                    }

                    switch (r.State)
                    {
                        case HordeRiderState.Leaping:
                        {
                            float t = r.StateTime / math.max(r.Duration, 0.01f);
                            r.Feet = HordeRiding.LeapPoint(r.From, at, s.LeapArc, t);
                            float2 dir = at.xz - r.From.xz;
                            if (math.lengthsq(dir) > 1e-4f)
                                r.Heading = math.atan2(dir.x, dir.y);
                            if (t >= 1f)
                                SetState(ref r, HordeRiderState.Clinging);
                            break;
                        }
                        case HordeRiderState.Clinging:
                            r.Feet = at;
                            r.Heading = yaw;
                            if (r.StateTime >= s.ClingTime)
                            {
                                SetState(ref r, HordeRiderState.Tearing);
                                r.StrikeTimer = HordeRiding.StrikeInterval(s, r.Slot, r.Strikes) * 0.5f;
                            }

                            break;
                        default: // tearing
                            r.Feet = at;
                            r.Heading = yaw;
                            r.StrikeTimer -= dt;
                            if (r.StrikeTimer <= 0f)
                            {
                                float damage = s.StrikeDamage * simulation.GetDamageScale(r.Slot) * _options.damageScale;
                                _damage += damage;
                                r.Strikes++;
                                r.StrikeTimer += HordeRiding.StrikeInterval(s, r.Slot, r.Strikes);
                                HordeRiderInfo info = Info(r, r.State);
                                info.Damage = damage;
                                HordeRiderEvents.RaiseStruck(info);
                            }

                            break;
                    }

                    simulation.SetRidePose(r.Slot, r.Feet, r.Heading);
                    n++;
                }

                _leapTimer -= dt;
                _scanTimer -= dt;
                if (_scanTimer <= 0f)
                {
                    _scanTimer = SCAN_INTERVAL;
                    if (_bound && _options.Active && _leapTimer <= 0f)
                        TryLeap(simulation, s, speed, yawRate);
                }
            }
        }

        /// <summary>Damage the riders dealt since the last call (the horde adds it to its bite damage on the tank).</summary>
        public float TakeDamage()
        {
            float d = _damage;
            _damage = 0f;
            return d;
        }

        /// <summary>Uploads and draws the riders at the pose of this frame.</summary>
        public void Draw(Bounds bounds)
        {
            for (int k = 0; k < HordeKinds.COUNT; k++)
                _drawn[k] = 0;
            for (int n = 0; n < _count; n++)
            {
                Rider r = _riders[n];
                int row = math.clamp(r.Kind, 0, HordeKinds.COUNT - 1);
                var pose = new HordeRiderPose { Feet = r.Feet, Heading = r.Heading, Clip = ClipOf(r.State), ClipTime = r.AnimTime };
                IHordeRiderAnimator animator = HordeRiderEvents.Animator;
                if (animator != null)
                    animator.Pose(Info(r, r.State), ref pose);
                float3 frame = pose.Clip.Sample(pose.ClipTime);
                _instances[row * MAX_RIDERS + _drawn[row]++] = new HordeInstance
                {
                    PositionHeading = new float4(pose.Feet, pose.Heading),
                    Animation = new float4(frame, row)
                };
            }

            for (int k = 0; k < HordeKinds.COUNT; k++)
            {
                if (_renderers[k] == null || _drawn[k] == 0)
                    continue;
                _renderers[k].Upload(_instances, k * MAX_RIDERS, 0, _drawn[k]);
                _renderers[k].Draw(_drawn[k], bounds);
            }
        }

        /// <summary>Draws the riders as they were (the game is paused).</summary>
        public void DrawPaused(Bounds bounds)
        {
            for (int k = 0; k < HordeKinds.COUNT; k++)
                if (_renderers[k] != null && _drawn[k] > 0)
                    _renderers[k].Draw(_drawn[k], bounds);
        }

        /// <summary>Forgets every rider (the horde was cleared for a new arena: their slots are gone).</summary>
        public void Clear()
        {
            for (int n = 0; n < _count; n++)
            {
                HordeRiderInfo info = Info(_riders[n], HordeRiderState.None);
                info.Cause = HordeShakeCause.TankLost;
                HordeRiderEvents.RaiseStateChanged(info);
            }

            _count = 0;
            for (int p = 0; p < _points.Length; p++)
                _points[p].Taken = false;
            for (int k = 0; k < HordeKinds.COUNT; k++)
                _drawn[k] = 0;
            _damage = 0f;
        }

        public void Dispose()
        {
            for (int k = 0; k < _renderers.Length; k++)
            {
                _renderers[k]?.Dispose();
                _renderers[k] = null;
            }

            if (_instances.IsCreated)
                _instances.Dispose();
        }

        // ------------------------------------------------------------------ leaps

        private void TryLeap(HordeSimulation simulation, in HordeRidingSettings s, float tankSpeed, float yawRate)
        {
            int max = math.min(s.MaxRiders, MAX_RIDERS);
            if (_count >= max)
                return;
            float chance = HordeRiding.LeapChance(s, tankSpeed, yawRate) * SCAN_INTERVAL;
            if (chance <= 0f)
                return;

            Quaternion inverse = Quaternion.Inverse(_tank.rotation);
            Vector3 centre = _tank.TransformPoint(_hullCenter);
            float time = GameTicks.Time;
            int high = simulation.HighWater;
            for (int i = 0; i < high; i++)
            {
                if (!simulation.IsSlotLive(i))
                    continue;
                HordeState state = simulation.GetState(i);
                if (state != HordeState.Walk && state != HordeState.Attack)
                    continue;
                if (!_options.Leaps(simulation.GetKindOf(i)))
                    continue;
                float3 p = simulation.GetTargetPosition(i);
                Vector3 local = inverse * ((Vector3)p - centre);
                if (local.y > _hullHalf.y || local.y < -_hullHalf.y - 1.5f)
                    continue;
                float2 q = math.max(math.abs(new float2(local.x, local.z)) - new float2(_hullHalf.x, _hullHalf.z), 0f);
                if (math.length(q) > s.LeapRange)
                    continue;
                if (_random.NextFloat() >= chance || FellRecently(i, simulation.Version(i), time, s.RejumpDelay))
                    continue;
                int point = NearestFreePoint(p);
                if (point < 0 || !simulation.BeginRide(i))
                    return;

                _points[point].Taken = true;
                PointPose((HordeAttachPoint)point, out float3 to, out _);
                ref Rider r = ref _riders[_count++];
                r = new Rider
                {
                    Slot = i,
                    Version = simulation.Version(i),
                    Kind = (byte)simulation.GetKindOf(i),
                    State = HordeRiderState.None,
                    Point = (HordeAttachPoint)point,
                    Duration = HordeRiding.LeapTime(s, math.distance(p, to)),
                    Grip = 1f,
                    LastHealth = simulation.GetHealth(i),
                    AnimTime = simulation.GetAnimTime(i),
                    From = p,
                    GroundY = p.y,
                    Feet = p,
                    Heading = simulation.GetHeading(i)
                };
                SetState(ref r, HordeRiderState.Leaping);
                _leapTimer = s.LeapGap;
                return; // one leap a scan: they come one by one
            }
        }

        private int NearestFreePoint(float3 from)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            for (int p = 0; p < _points.Length; p++)
            {
                if (_points[p].Taken)
                    continue;
                PointPose((HordeAttachPoint)p, out float3 at, out _);
                float d2 = math.distancesq(at, from);
                if (d2 >= bestSq)
                    continue;
                bestSq = d2;
                best = p;
            }

            return best;
        }

        private bool FellRecently(int slot, int version, float time, float delay)
        {
            for (int k = 0; k < FALLEN_RING; k++)
                if (_fallen[k].x == slot && _fallen[k].y == version && time - _fallenTime[k] < delay)
                    return true;
            return false;
        }

        // ------------------------------------------------------------------ falls and deaths

        private void ShakeOff(ref Rider r, float3 velocity, HordeShakeCause cause)
        {
            HordeRidingSettings s = _options.Settings;
            float3 away = float3.zero;
            if (_bound && _tank != null)
            {
                Vector3 centre = _tank.TransformPoint(_hullCenter);
                away = (float3)((Vector3)r.Feet - centre);
            }

            away.y = 0f;
            away = math.normalizesafe(away, new float3(0f, 0f, 1f));
            r.Velocity = velocity + away * s.ThrowOut + new float3(0f, s.ThrowUp, 0f);
            r.Cause = cause;
            _points[(int)r.Point].Taken = false;
            SetState(ref r, HordeRiderState.ShakenOff);
        }

        /// <summary>One step of a fall; true when it landed (back to the crowd).</summary>
        private bool Fall(HordeSimulation simulation, HordeNavGrid grid, ref Rider r, float dt)
        {
            r.Velocity.y -= HordeRiding.GRAVITY * dt;
            r.Feet += r.Velocity * dt;
            float ground = grid != null ? grid.HeightAt(r.Feet) : float.NaN;
            if (float.IsNaN(ground))
                ground = r.GroundY; // no grid height here (a hole, the tank's own cells, off the map): the ground it leapt from
            bool landed = !float.IsNaN(ground) && r.Feet.y <= ground && r.Velocity.y < 0f;
            if (!landed && r.StateTime < MAX_FALL)
            {
                simulation.SetRidePose(r.Slot, r.Feet, r.Heading);
                return false;
            }

            float3 feet = r.Feet;
            feet.y = ground;
            if (grid != null)
            {
                // Thrown past the map's edge: it lands on the edge.
                float2 min = grid.Settings.Origin + 0.25f;
                float2 max = grid.Settings.Origin + (float2)grid.Size * grid.CellSize - 0.25f;
                feet.xz = math.clamp(feet.xz, min, max);
            }

            simulation.EndRide(r.Slot, feet, r.Heading);
            _fallen[_fallenHead] = new int2(r.Slot, r.Version);
            _fallenTime[_fallenHead] = GameTicks.Time;
            _fallenHead = (_fallenHead + 1) % FALLEN_RING;
            r.Feet = feet;
            SetState(ref r, HordeRiderState.None);
            return true;
        }

        /// <summary>
        /// A rider died on the tank: its death (already freed by the step) is moved to the ground by the hull, so the corpse
        /// does not hang in the air; the goo flies from where it held on and stains the hull.
        /// </summary>
        private void RiderDied(HordeSimulation simulation, HordeNavGrid grid, in Rider r)
        {
            if (r.State != HordeRiderState.ShakenOff)
                _points[(int)r.Point].Taken = false;

            float3 drop = r.Feet;
            if (_bound && _tank != null)
            {
                Vector3 centre = _tank.TransformPoint(_hullCenter);
                float3 away = (float3)((Vector3)r.Feet - centre);
                away.y = 0f;
                away = math.normalizesafe(away, new float3(0f, 0f, 1f));
                // Out past the hull's side along the way from its centre.
                Vector3 local = Quaternion.Inverse(_tank.rotation) * (Vector3)away;
                float reach = math.min(
                    math.abs(local.x) > 1e-3f ? _hullHalf.x / math.abs(local.x) : float.MaxValue,
                    math.abs(local.z) > 1e-3f ? _hullHalf.z / math.abs(local.z) : float.MaxValue);
                float3 c = (float3)centre;
                drop = new float3(c.x, r.Feet.y, c.z) + away * (reach + DROP_OUT);
            }

            float ground = grid != null ? grid.HeightAt(drop) : float.NaN;
            drop.y = float.IsNaN(ground) ? r.GroundY : ground;

            NativeArray<HordeDeathEvent> deaths = simulation.Deaths;
            for (int i = 0; i < deaths.Length; i++)
            {
                if (deaths[i].Index != r.Slot)
                    continue;
                HordeDeathEvent death = deaths[i];
                death.Position = drop;
                deaths[i] = death;
            }

            if (r.State != HordeRiderState.ShakenOff && r.State != HordeRiderState.Leaping)
            {
                Vector3 at = (Vector3)r.Feet + Vector3.up * 0.4f;
                VoxelSpray.Goo(at, Vector3.up, 36, 5f, 0.6f, _goo, _gooSize, _body != null ? _body.linearVelocity * 0.5f : Vector3.zero, 0.3f);
                TankBloodStains.Splat(r.Feet, 0.45f);
            }

            HordeRiderEvents.RaiseDied(Info(r, r.State));
        }

        private void Remove(int n)
        {
            _count--;
            _riders[n] = _riders[_count];
        }

        // ------------------------------------------------------------------ tank

        private void ReadMotion(float dt, out float3 velocity, out float yawRate, out float turretRate, out float jolt)
        {
            velocity = float3.zero;
            yawRate = 0f;
            turretRate = 0f;
            jolt = 0f;
            if (!_bound || _tank == null)
            {
                _bound = false;
                return;
            }

            Vector3 v = _body != null ? _body.linearVelocity : Vector3.zero;
            velocity = v;
            yawRate = _body != null ? _body.angularVelocity.y : 0f;
            float turretYaw = _turret != null ? _turret.localEulerAngles.y : 0f;
            if (_hasLast && dt > 1e-4f)
            {
                // Horizontal only: a ram or a hard stop, not the bumps of the ground under the tracks.
                Vector3 change = v - _lastVelocity;
                change.y = 0f;
                jolt = change.magnitude / dt;
                turretRate = Mathf.DeltaAngle(_lastTurretYaw, turretYaw) * Mathf.Deg2Rad / dt;
            }

            _lastVelocity = v;
            _lastTurretYaw = turretYaw;
            _hasLast = true;
        }

        /// <summary>Velocity of the hull at <paramref name="at"/> (its spin included).</summary>
        private float3 PointVelocity(float3 at, float3 velocity)
        {
            if (_body == null)
                return velocity;
            return velocity + (float3)Vector3.Cross(_body.angularVelocity, (Vector3)at - _body.worldCenterOfMass);
        }

        private void PointPose(HordeAttachPoint point, out float3 feet, out float yaw)
        {
            Point p = _points[(int)point];
            Transform parent = p.OnTurret && _turret != null ? _turret : _tank;
            if (parent == null)
            {
                feet = float3.zero;
                yaw = 0f;
                return;
            }

            feet = parent.TransformPoint(p.Local);
            Vector3 forward = parent.rotation * new Vector3(Mathf.Sin(p.Yaw), 0f, Mathf.Cos(p.Yaw));
            yaw = math.atan2(forward.x, forward.z);
        }

        /// <summary>
        /// The six attach points over the hull box: feet on the tank's surface found by rays down onto its colliders
        /// (the hull box's top when none answer); the turret's on its roof, in the turret's space.
        /// </summary>
        private void MeasurePoints()
        {
            Vector3 c = _hullCenter;
            Vector3 h = _hullHalf;
            Set(HordeAttachPoint.FrontLeft, new Vector3(c.x - 0.45f * h.x, 0f, c.z + 0.78f * h.z), math.PI);
            Set(HordeAttachPoint.FrontRight, new Vector3(c.x + 0.45f * h.x, 0f, c.z + 0.78f * h.z), math.PI);
            Set(HordeAttachPoint.Left, new Vector3(c.x - 0.82f * h.x, 0f, c.z - 0.15f * h.z), math.PI * 0.5f);
            Set(HordeAttachPoint.Right, new Vector3(c.x + 0.82f * h.x, 0f, c.z - 0.15f * h.z), -math.PI * 0.5f);
            Set(HordeAttachPoint.Rear, new Vector3(c.x, 0f, c.z - 0.85f * h.z), 0f);

            ref Point top = ref _points[(int)HordeAttachPoint.Turret];
            top.OnTurret = _turret != null;
            top.Yaw = 0f;
            if (_turret != null)
            {
                Vector3 world = _turret.position;
                float y = SurfaceY(world, _turret, out bool hit);
                if (!hit)
                    y = TurretTop();
                top.Local = _turret.InverseTransformPoint(new Vector3(world.x, y, world.z)) + new Vector3(0f, 0f, -0.25f);
            }
            else
            {
                top.Local = new Vector3(c.x, c.y + h.y, c.z - 0.2f * h.z);
            }
        }

        private void Set(HordeAttachPoint point, Vector3 local, float yaw)
        {
            Vector3 world = _tank.TransformPoint(new Vector3(local.x, _hullCenter.y, local.z));
            float y = SurfaceY(world, null, out bool hit);
            Vector3 feet = hit ? _tank.InverseTransformPoint(new Vector3(world.x, y, world.z)) : new Vector3(local.x, _hullCenter.y + _hullHalf.y, local.z);
            ref Point p = ref _points[(int)point];
            p.Local = new Vector3(local.x, feet.y, local.z);
            p.Yaw = yaw;
            p.OnTurret = false;
        }

        /// <summary>Highest point of the tank's colliders (of <paramref name="under"/> only, when given) under a ray down through <paramref name="world"/>.</summary>
        private float SurfaceY(Vector3 world, Transform under, out bool hit)
        {
            hit = false;
            float best = float.MinValue;
            var ray = new Ray(new Vector3(world.x, world.y + 6f, world.z), Vector3.down);
            Transform root = under != null ? under : _tank;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled)
                    continue;
                if (!collider.Raycast(ray, out RaycastHit h, 12f) || h.point.y <= best)
                    continue;
                best = h.point.y;
                hit = true;
            }

            return best;
        }

        private float TurretTop()
        {
            float top = _turret.position.y;
            foreach (Renderer r in _turret.GetComponentsInChildren<Renderer>())
                top = Mathf.Max(top, r.bounds.max.y);
            return top;
        }

        // ------------------------------------------------------------------ states

        private HordeClip ClipOf(HordeRiderState state)
        {
            switch (state)
            {
                case HordeRiderState.Leaping:
                    return _options.leapClip.count > 0 ? _options.leapClip : _run;
                case HordeRiderState.Tearing:
                    return _options.tearClip.count > 0 ? _options.tearClip : _attack;
                case HordeRiderState.ShakenOff:
                    return _options.fallClip.count > 0 ? _options.fallClip : _idle;
                default:
                    return _options.clingClip.count > 0 ? _options.clingClip : _idle;
            }
        }

        private static void SetState(ref Rider r, HordeRiderState state)
        {
            HordeRiderState previous = r.State;
            r.State = state;
            r.StateTime = 0f;
            HordeRiderInfo info = Info(r, state);
            info.Previous = previous;
            HordeRiderEvents.RaiseStateChanged(info);
        }

        private static HordeRiderInfo Info(in Rider r, HordeRiderState state) => new()
        {
            Slot = r.Slot,
            Version = r.Version,
            Kind = r.Kind,
            State = state,
            Previous = r.State,
            Point = r.Point,
            Feet = r.Feet,
            Heading = r.Heading,
            StateTime = r.StateTime,
            Grip = r.Grip,
            Cause = r.Cause
        };
    }
}
