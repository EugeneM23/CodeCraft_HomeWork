using System.Collections.Generic;
using Game.Horde;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Where the elite's body really goes, after the entity's walk (<c>TransformMoveBehaviour</c>) has moved it: it does
    /// not walk into a wall of the grid (a step the grid does not allow is taken back, sliding along the free axis), it is
    /// pushed out of the tank's hull (it stands against the tank, not in it), and it stands on the grid's height with a
    /// little smoothing (the dug ground under it); two elites keep apart. No physics: its Rigidbody is kinematic, the tank's crush finds it
    /// with an overlap box. A leap (<see cref="TryLeapAway"/>) flies an arc over the ground and owns the body until it
    /// lands. No managed allocations.
    /// </summary>
    public sealed class EliteMotor : ILeapActuator
    {
        /// <summary>How far the body's middle keeps from the hull (its capsule's radius and a hand's breadth).</summary>
        public const float BODY_CLEARANCE = 0.6f;

        /// <summary>How fast the feet follow the ground, 1/s.</summary>
        private const float GROUND_RATE = 12f;

        /// <summary>Farther than this from the ground (a spawn, a teleport) the body is put on it at once.</summary>
        private const float SNAP_HEIGHT = 3f;

        /// <summary>Top of the leap's arc over the straight line, m.</summary>
        private const float LEAP_HEIGHT = 1.2f;

        /// <summary>Landing at most this much higher, or this much lower, than the take-off, m.</summary>
        private const float LEAP_MAX_RISE = 1f;
        private const float LEAP_MAX_DROP = 2.5f;

        /// <summary>Metres between the checks of the ground under the leap's line.</summary>
        private const float LEAP_SAMPLE = 0.5f;

        /// <summary>Directions tried for a landing, degrees off straight away from the blast.</summary>
        private static readonly float[] LeapAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };

        /// <summary>Half the distance two elites keep between their middles, m.</summary>
        private const float SEPARATION_RADIUS = 0.75f;

        /// <summary>The elites of the level, which keep apart (a handful: every pair is checked).</summary>
        private static readonly List<EliteMotor> s_motors = new();

        private Transform _transform;
        private LevelNav _nav;
        private PlayerTank _tank;
        private Vector3 _last;
        private bool _hasLast;
        private Vector3 _velocity;

        private bool _leaping;
        private Vector3 _from;
        private Vector3 _to;
        private float _t;
        private float _duration;

        public EliteMotor(Transform transform) => _transform = transform;

        public Transform Transform => _transform;

        public bool IsLeaping => _leaping;

        /// <summary>The body's speed over the last tick (the leap and the ground's lift included).</summary>
        public Vector3 Velocity => _velocity;

        /// <summary>Where the current leap lands (valid while <see cref="IsLeaping"/>, kept after it).</summary>
        public Vector3 LeapTarget => _to;

        private HordeNavGrid Grid => _nav?.Grid;

        /// <summary>Takes the level's grid and the player's vehicle, and joins the elites that keep apart.</summary>
        public void Bind(LevelNav nav, PlayerTank tank)
        {
            _nav = nav;
            _tank = tank;
            if (!s_motors.Contains(this))
                s_motors.Add(this);
        }

        /// <summary>Leaves the elites that keep apart (death).</summary>
        public void Release()
        {
            s_motors.Remove(this);
            _leaping = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_motors.Clear();

        // Two elites never stand in one place: each steps out of the other's way by half of what they overlap
        private void KeepApart(ref Vector3 p)
        {
            float min = 2f * SEPARATION_RADIUS;
            for (int i = 0; i < s_motors.Count; i++)
            {
                EliteMotor other = s_motors[i];
                if (other == this || other._transform == null || !other._hasLast)
                    continue;
                Vector3 d = p - other._last;
                d.y = 0f;
                float distance = d.magnitude;
                if (distance >= min)
                    continue;
                // On top of each other: the one that joined later steps aside, along a fixed way
                Vector3 away = distance > 1e-4f ? d / distance
                    : Quaternion.AngleAxis(137.5f * s_motors.IndexOf(this), Vector3.up) * Vector3.forward;
                p += away * ((min - distance) * 0.5f);
            }
        }

        /// <summary>Puts the body on the ground under it now (a spawn).</summary>
        public void Snap()
        {
            Vector3 p = _transform.position;
            HordeNavGrid grid = Grid;
            float h = grid != null ? grid.HeightAt(p) : float.NaN;
            if (!float.IsNaN(h))
                p.y = h;
            _transform.position = p;
            _last = p;
            _hasLast = true;
            _velocity = Vector3.zero;
        }

        /// <summary>Once a frame, after the entity's walk moved the transform.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || _transform == null)
                return;

            Vector3 p = _transform.position;
            if (_leaping)
            {
                _t += dt / _duration;
                float k = Mathf.Min(1f, _t);
                Vector3 q = Vector3.Lerp(_from, _to, k);
                q.y += 4f * LEAP_HEIGHT * k * (1f - k);
                if (k >= 1f)
                    _leaping = false;
                Set(q, dt);
                return;
            }

            KeepApart(ref p);

            HordeNavGrid grid = Grid;
            if (_hasLast && grid != null)
            {
                int2 from = grid.ClampCell(_last);
                if (!Steppable(grid, from, grid.ClampCell(p)))
                {
                    var alongX = new Vector3(p.x, p.y, _last.z);
                    var alongZ = new Vector3(_last.x, p.y, p.z);
                    if (Steppable(grid, from, grid.ClampCell(alongX)))
                        p = alongX;
                    else if (Steppable(grid, from, grid.ClampCell(alongZ)))
                        p = alongZ;
                    else
                        p = new Vector3(_last.x, p.y, _last.z);
                }
            }

            _tank?.PushOut(ref p, BODY_CLEARANCE);

            float h = grid != null ? grid.HeightAt(p) : float.NaN;
            if (!float.IsNaN(h))
                p.y = Mathf.Abs(h - p.y) > SNAP_HEIGHT ? h : Mathf.Lerp(p.y, h, 1f - Mathf.Exp(-GROUND_RATE * dt));

            Set(p, dt);
        }

        public bool TryLeapAway(Vector3 from, float minDistance, float maxDistance, float duration)
        {
            if (_transform == null || _leaping)
                return false;

            Vector3 p = _transform.position;
            Vector3 away = p - from;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f)
            {
                away = -_transform.forward;
                away.y = 0f;
            }

            if (away.sqrMagnitude < 1e-6f)
                away = Vector3.back;
            away.Normalize();

            minDistance = Mathf.Max(0.5f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);
            for (int d = 0; d < 3; d++)
            {
                float distance = Mathf.Lerp(maxDistance, minDistance, d * 0.5f);
                for (int a = 0; a < LeapAngles.Length; a++)
                {
                    Vector3 direction = Quaternion.AngleAxis(LeapAngles[a], Vector3.up) * away;
                    Vector3 landing = p + direction * distance;
                    if (!Landable(p, landing, out float y))
                        continue;

                    _from = p;
                    _to = new Vector3(landing.x, y, landing.z);
                    _t = 0f;
                    _duration = Mathf.Max(0.1f, duration);
                    _leaping = true;
                    return true;
                }
            }

            return false;
        }

        // Ground to land on: not much higher or lower, nothing tall on the way (a house), not in the tank's hull
        private bool Landable(Vector3 start, Vector3 landing, out float y)
        {
            y = start.y;
            if (_tank != null && _tank.IsPresent && _tank.DistanceToHull(landing) < BODY_CLEARANCE)
                return false;

            HordeNavGrid grid = Grid;
            if (grid == null)
                return true;

            float h = grid.HeightAt(landing);
            if (float.IsNaN(h) || h - start.y > LEAP_MAX_RISE || start.y - h > LEAP_MAX_DROP)
                return false;

            Vector3 line = landing - start;
            line.y = 0f;
            int samples = Mathf.Max(1, Mathf.CeilToInt(line.magnitude / LEAP_SAMPLE));
            float top = Mathf.Max(start.y, h) + LEAP_HEIGHT * 0.8f;
            for (int s = 1; s < samples; s++)
            {
                float under = grid.HeightAt(start + line * ((float)s / samples));
                if (!float.IsNaN(under) && under > top)
                    return false;
            }

            y = h;
            return true;
        }

        private static bool Steppable(HordeNavGrid grid, int2 from, int2 to)
        {
            int2 d = to - from;
            if (math.all(d == 0) || math.any(math.abs(d) > 1))
                return true;                 // the same cell, or a jump of more than a cell (placed from outside)
            if (!grid.HasGround(from))
                return true;                 // standing nowhere the grid knows: no wall holds it
            return grid.CanStep(from, to);
        }

        private void Set(Vector3 position, float dt)
        {
            if (_transform.position != position)
                _transform.position = position;
            _velocity = _hasLast ? (position - _last) / dt : Vector3.zero;
            _last = position;
            _hasLast = true;
        }
    }
}
