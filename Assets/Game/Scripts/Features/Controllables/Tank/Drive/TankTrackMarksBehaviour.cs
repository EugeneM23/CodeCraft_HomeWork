using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Presses the tracks into mud, into the MudSurface the wheels stand on. Each side, every frame:
    ///   - the run on the ground — from under the idler, past every grounded road wheel, to under
    ///     the sprocket — is pressed at least one pass deep, so a rut is there the moment a track
    ///     is, turning on the spot included;
    ///   - the ground the leading end swept since last frame is dug one pass deeper, so every
    ///     pass over the same ground digs further: two reach the bottom;
    ///   - clumps of mud fly off the trailing end, more the faster the track runs;
    ///   - in a turn both ends slide sideways and shove chunks of mud aside;
    ///   - the mud churned up cakes onto the tank (TankDirtBehaviour).
    ///
    /// Positions come from the drawn wheels (TankTrackView), not the physics step, so the rut lies
    /// exactly under the drawn track instead of a step behind it.
    /// </summary>
    public sealed class TankTrackMarksBehaviour : IEntityInit, IEntityTick
    {
        /// <summary>Share of the mud one pass presses away.</summary>
        private const float PASS_DEPTH = 0.5f;

        /// <summary>Slowest (m/s) a track counts as rolling onto fresh ground; slower is a jitter.</summary>
        private const float MIN_SPEED = 0.05f;

        /// <summary>
        /// Furthest (m) a lead may get past its frontier at once and still be swept rather than
        /// jumped. More than a wheel's spacing (≈ 0.5 m): the end wheel landing again moves the
        /// lead that far in one frame.
        /// </summary>
        private const float MAX_STEP = 1.5f;

        /// <summary>Track width (m) if the rig has no links to read it from.</summary>
        private const float DEFAULT_WIDTH = 0.5f;

        /// <summary>Clumps a track throws per metre it rolls over the mud.</summary>
        private const float CLUMPS_PER_METRE = 0.8f;

        /// <summary>Fewest clumps a second a rolling track throws, however slowly it creeps.</summary>
        private const float MIN_CLUMPS_PER_SECOND = 0.5f;

        /// <summary>Share of the track's speed a clump leaves the trailing end with, backwards.</summary>
        private const float CLUMP_BACK = 0.15f;

        /// <summary>Backward speed (m/s) a clump gets on top of that: they fly back even when crawling.</summary>
        private const float THROW_BACK_MIN = 0.3f;
        private const float THROW_BACK_MAX = 0.9f;

        /// <summary>Share of thrown clumps that drop back into the rut instead of past it.</summary>
        private const float INTO_RUT_SHARE = 0.5f;

        /// <summary>Outward speed (m/s) of a thrown clump: enough to land past the rut's berm, not far beyond.</summary>
        private const float THROW_SIDE_MIN = 0.9f;
        private const float THROW_SIDE_MAX = 1.4f;

        /// <summary>Upward speed (m/s) of a thrown clump: a short, low arc: heavy wet mud drops close by.</summary>
        private const float THROW_UP_MIN = 1.4f;
        private const float THROW_UP_MAX = 2f;

        /// <summary>
        /// Clumps an end of a track shoves aside per metre it slides sideways in a turn. Sliding
        /// ploughs far more mud than rolling.
        /// </summary>
        private const float SCRAPE_PER_METRE = 1.2f;

        /// <summary>Share of the slide's speed a scraped clump leaves with, outwards.</summary>
        private const float SCRAPE_OUT = 0.25f;

        /// <summary>
        /// Sideways speed (m/s) range a chunk leaves the track's outer edge with: enough to clear the
        /// rut and its berm (≈ 0.5–1 m out) instead of dropping back onto it.
        /// </summary>
        private const float CLUMP_OUT_MIN = 0.8f;
        private const float CLUMP_OUT_MAX = 1.4f;

        /// <summary>Random spread (m/s) on a chunk's velocity.</summary>
        private const float CLUMP_SPREAD = 0.2f;

        /// <summary>Dirt (TankDirtBehaviour; 1 = most spots) per metre a track rolls on mud.</summary>
        private const float DIRT_PER_ROLLED_METRE = 0.012f;

        /// <summary>Dirt per metre a track end slides sideways on mud.</summary>
        private const float DIRT_PER_SLID_METRE = 0.045f;

        private readonly TankTrackRig _left;
        private readonly TankTrackRig _right;
        private readonly TankDirtBehaviour _dirt;

        private Transform _hull;
        private Rigidbody _body;
        private TankSuspension _suspension;
        private Side _leftSide;
        private Side _rightSide;
        private float _width;

        private readonly List<Vector3> _points = new();

        private sealed class Side
        {
            public TankWheel[] Wheels;
            public Vector3[] Contacts;
            public bool[] Grounded;

            // Per wheel: the collider it was last seen standing on, and that collider's MudSurface
            // (null for none). See SurfaceUnder.
            public Collider[] Grounds;
            public MudSurface[] Surfaces;

            public float HalfTrack;
            public int LeadEnd;
            public Vector3 Lead;
            public float ClumpsOwed;
            public float ScrapeOwed;
        }

        public TankTrackMarksBehaviour(TankTrackRig left, TankTrackRig right, TankDirtBehaviour dirt = null)
        {
            _left = left;
            _right = right;
            _dirt = dirt;
        }

        public void Init(IEntity entity)
        {
            _hull = entity.GetTransform();
            _body = entity.GetRiggedBody();
            _suspension = entity.GetSuspension();

            _leftSide = MakeSide(_suspension.Wheels, -1);
            _rightSide = MakeSide(_suspension.Wheels, 1);

            Transform link = _left?.Links is { Length: > 0 } ? _left.Links[0] : null;
            _width = link != null ? link.localScale.x : DEFAULT_WIDTH;
        }

        /// <summary>One side's wheels, front to rear, as TankTrackView orders them.</summary>
        private static Side MakeSide(TankWheel[] wheels, int side)
        {
            List<TankWheel> list = new();
            float x = 0f;
            foreach (TankWheel wheel in wheels)
            {
                if (wheel.Side != side)
                    continue;
                list.Add(wheel);
                x += Mathf.Abs(wheel.Mount.localPosition.x);
            }

            list.Sort((a, b) => b.Mount.localPosition.z.CompareTo(a.Mount.localPosition.z));
            return new Side
            {
                Wheels = list.ToArray(),
                Contacts = new Vector3[list.Count],
                Grounded = new bool[list.Count],
                Grounds = new Collider[list.Count],
                Surfaces = new MudSurface[list.Count],
                HalfTrack = list.Count > 0 ? x / list.Count : 0f
            };
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            Vector3 up = _hull.up;
            Vector3 forward = _hull.forward;
            float speed = Vector3.Dot(_body.linearVelocity, forward);
            float yawRate = Vector3.Dot(_body.angularVelocity, up);

            Stamp(_leftSide, -1, speed, yawRate, up, forward, deltaTime);
            Stamp(_rightSide, 1, speed, yawRate, up, forward, deltaTime);
        }

        private void Stamp(Side side, int sign, float speed, float yawRate, Vector3 up, Vector3 forward, float deltaTime)
        {
            // The first grounded wheel, front to rear, that stands on mud.
            MudSurface surface = null;
            for (int i = 0; i < side.Wheels.Length && surface == null; i++)
                surface = SurfaceUnder(side, i);

            if (surface == null)
            {
                side.LeadEnd = 0;
                return;
            }

            // Only read on mud: off it nothing uses them, and each contact is a native read.
            for (int i = 0; i < side.Wheels.Length; i++)
            {
                TankWheel wheel = side.Wheels[i];
                side.Grounded[i] = wheel.Grounded;
                side.Contacts[i] = wheel.Visual.position - up * _suspension.WheelRadius;
            }

            TrackMarkMath.Points(side.Contacts, side.Grounded, forward, _suspension.WheelRadius, _points);

            for (int i = 1; i < _points.Count; i++)
                surface.Press(_points[i - 1], _points[i], _width, PASS_DEPTH);

            // A point on the left track moves forward faster in a right turn, and the reverse.
            float trackSpeed = speed - sign * yawRate * side.HalfTrack;
            if (Mathf.Abs(trackSpeed) < MIN_SPEED)
                trackSpeed = 0f;

            int end = TrackMarkMath.Lead(_points, trackSpeed, out Vector3 lead);
            if (TrackMarkMath.Sweep(ref side.LeadEnd, ref side.Lead, end, lead, forward, MAX_STEP, out Vector3 from))
                surface.Dig(from, lead, _width, PASS_DEPTH);

            if (end != 0)
                ThrowClumps(side, surface, sign, end, trackSpeed, up, forward, deltaTime);

            float slid = ScrapeClumps(side, surface, sign, yawRate, up, forward, deltaTime);
            _dirt?.Splash(Mathf.Abs(trackSpeed) * deltaTime * DIRT_PER_ROLLED_METRE + slid * DIRT_PER_SLID_METRE);
        }

        /// <summary>
        /// In a turn both ends of a track slide sideways through the mud — the front one way, the
        /// rear the other — and shove it aside in chunks. They always fly out past the track's outer
        /// edge, never back onto the ruts. Returns how far (m) the ends slid this frame.
        /// </summary>
        private float ScrapeClumps(Side side, MudSurface surface, int sign, float yawRate, Vector3 up, Vector3 forward,
            float deltaTime)
        {
            if (Mathf.Abs(yawRate) < 0.05f || _points.Count < 2)
                return 0f;

            Vector3 pivot = _hull.position;
            Vector3 spin = up * yawRate;
            Vector3 outward = Vector3.Cross(up, forward) * sign;
            float slid = 0f;

            for (int e = 0; e < 2; e++)
            {
                Vector3 at = e == 0 ? _points[0] : _points[^1];
                Vector3 slide = Vector3.Cross(spin, at - pivot);
                slide -= up * Vector3.Dot(slide, up);
                float speed = slide.magnitude;
                if (speed < MIN_SPEED)
                    continue;

                float distance = speed * deltaTime;
                slid += distance;
                side.ScrapeOwed += distance * SCRAPE_PER_METRE;

                int count = Mathf.FloorToInt(side.ScrapeOwed);
                if (count == 0)
                    continue;
                side.ScrapeOwed -= count;

                // Ahead of or behind the end, the way it slides, and out to the side.
                Vector3 along = forward * (Vector3.Dot(slide, forward) * 0.5f);
                for (int i = 0; i < count; i++)
                {
                    Vector3 velocity = outward * (speed * SCRAPE_OUT + Random.Range(CLUMP_OUT_MIN, CLUMP_OUT_MAX))
                                       + along + up * (1.2f + speed * 0.15f);
                    Vector3 from = at + up * 0.12f + outward * (_width * Random.Range(0.4f, 0.7f));
                    surface.Throw(from, velocity, CLUMP_SPREAD, 1, 1.15f);
                }
            }

            return slid;
        }

        /// <summary>
        /// The MudSurface wheel <paramref name="i"/> stands on, or null. Looked up only when the wheel
        /// has come onto another collider: it was a GetComponent for every grounded wheel every frame,
        /// ten a frame off mud, which is all of the Playground.
        /// </summary>
        private static MudSurface SurfaceUnder(Side side, int i)
        {
            TankWheel wheel = side.Wheels[i];
            if (!wheel.Grounded || wheel.Ground == null)
                return null;

            if (wheel.Ground != side.Grounds[i])
            {
                side.Grounds[i] = wheel.Ground;
                wheel.Ground.TryGetComponent(out side.Surfaces[i]);
            }

            return side.Surfaces[i];
        }

        /// <summary>
        /// Mud flung off the track where it leaves the ground — the sprocket going forward, the
        /// idler in reverse — back against the way it moves, fanned out to the side and up, in a
        /// steady stream whenever it rolls, thicker the faster it runs.
        /// </summary>
        private void ThrowClumps(Side side, MudSurface surface, int sign, int end, float trackSpeed, Vector3 up,
            Vector3 forward, float deltaTime)
        {
            float rolled = Mathf.Abs(trackSpeed) * deltaTime;
            side.ClumpsOwed += Mathf.Max(rolled * CLUMPS_PER_METRE, MIN_CLUMPS_PER_SECOND * deltaTime);

            int count = Mathf.FloorToInt(side.ClumpsOwed);
            if (count == 0)
                return;
            side.ClumpsOwed -= count;

            Vector3 trailing = end > 0 ? _points[^1] : _points[0];
            Vector3 back = -forward * end;
            Vector3 outward = Vector3.Cross(up, forward) * sign;
            float speed = Mathf.Abs(trackSpeed);

            for (int i = 0; i < count; i++)
            {
                // Most from the track's outer edge, out past the rut and its berm; some drop straight
                // back into the rut behind. A little back, up in a short heavy arc.
                bool intoRut = Random.value < INTO_RUT_SHARE;
                Vector3 at = trailing + up * 0.15f +
                             outward * (_width * (intoRut ? Random.Range(-0.3f, 0.3f) : Random.Range(0.5f, 0.8f)));
                float sideways = intoRut ? Random.Range(-0.15f, 0.15f) : Random.Range(THROW_SIDE_MIN, THROW_SIDE_MAX);
                Vector3 velocity = back * (speed * CLUMP_BACK + Random.Range(THROW_BACK_MIN, THROW_BACK_MAX))
                                   + outward * sideways
                                   + up * (Random.Range(THROW_UP_MIN, THROW_UP_MAX) + speed * 0.05f);
                surface.Throw(at, velocity, CLUMP_SPREAD, 1);
            }
        }
    }
}
