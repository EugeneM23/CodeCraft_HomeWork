using Game.Horde;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// TEMPORARY bridge (combat team) until the AI team ships its own horde target source: the horde's monsters as
    /// an <see cref="IEnemyTargetSource"/> for the guns, turrets and skills.
    /// <para>
    /// The simulation's arrays may be read only while no step runs (<see cref="HordeSimulation.IsRunning"/>), and a
    /// step runs from the horde's tick to the start of the next frame. So the source keeps a snapshot — live slots,
    /// aim points, health — taken at most once a frame whenever it is asked while no step runs (always so in the
    /// physics steps, where bullets fly), and answers from it, at most a frame old. Slots are handed out lowest first
    /// and freed slots are reused first, so every live slot lies below the most monsters ever alive at once: the
    /// snapshot scans only that far, not the whole capacity.
    /// </para>
    /// <para>
    /// A hit adds up in the source (the simulation has no single-monster damage): when what was dealt reaches the
    /// monster's health it is killed (<see cref="HordeSimulation.Kill"/>; if a step runs, it is completed first —
    /// waited for here, since the kill must land now). Area damage goes to <see cref="HordeSimulation.AddAreaDamage"/>
    /// for the next step (its own falloff: half at the edge, not minFactor). A slot's version changes whenever the
    /// snapshot sees it die or come alive. No allocations after construction.
    /// </para>
    /// </summary>
    public sealed class HordeTargetSource : IEnemyTargetSource, IEnemyTargetRingQuery
    {
        /// <summary>Aim point above a monster's feet.</summary>
        private const float AIM_HEIGHT = 0.6f;

        /// <summary>Slots scanned past the most monsters ever alive at once.</summary>
        private const int SCAN_MARGIN = 32;

        /// <summary>
        /// A live slot farther than this (m, squared: 2 m) from its snapshot point within a frame holds a new monster.
        /// </summary>
        private const float REUSED_JUMP_SQ = 4f;

        /// <summary>Health above the snapshot's by more than this: a new monster (health does not regrow).</summary>
        private const float HEALTH_EPSILON = 0.01f;

        private static readonly ProfilerMarker SnapshotMarker = new("Combat.HordeSnapshot");

        private readonly HordeBehaviour _horde;
        private readonly HordeSimulation _simulation;

        private readonly bool[] _live;
        private readonly Vector3[] _aim;
        private readonly float[] _health;
        private readonly float[] _dealt;
        private readonly int[] _versions;
        private readonly int[] _liveList;
        private int _liveCount;
        private int _peak;
        private int _snapshotFrame = -1;

        public HordeTargetSource(HordeBehaviour horde)
        {
            _horde = horde;
            _simulation = horde.Simulation;
            int capacity = _simulation.Capacity;
            _live = new bool[capacity];
            _aim = new Vector3[capacity];
            _health = new float[capacity];
            _dealt = new float[capacity];
            _versions = new int[capacity];
            _liveList = new int[capacity];
        }

        public int AliveCount
        {
            get
            {
                Refresh();
                return _liveCount;
            }
        }

        public bool TryFindNearest(Vector3 from, float range, out int index, out int version, out Vector3 position) =>
            TryFindNearestBeyond(from, -1f, range, out index, out version, out position);

        public bool TryFindNearestBeyond(Vector3 from, float minRange, float maxRange, out int index, out int version,
            out Vector3 position)
        {
            Refresh();
            index = -1;
            version = 0;
            position = default;

            float bestSq = maxRange * maxRange;
            float minSq = minRange > 0f ? minRange * minRange : -1f;
            for (int n = 0; n < _liveCount; n++)
            {
                int i = _liveList[n];
                if (!_live[i])
                    continue;
                float sq = (_aim[i] - from).sqrMagnitude;
                if (sq > bestSq || sq <= minSq)
                    continue;
                bestSq = sq;
                index = i;
            }

            if (index < 0)
                return false;
            version = _versions[index];
            position = _aim[index];
            return true;
        }

        public bool IsAlive(int index, int version)
        {
            Refresh();
            return index >= 0 && index < _live.Length && _live[index] && _versions[index] == version;
        }

        public Vector3 GetPosition(int index) => index >= 0 && index < _aim.Length ? _aim[index] : default;

        public bool Raycast(Vector3 from, Vector3 to, float radius, out int index, out int version, out Vector3 point)
        {
            Refresh();
            index = -1;
            version = 0;
            point = to;

            float body = _simulation.Settings.Radius + radius;
            float best = float.MaxValue;
            for (int n = 0; n < _liveCount; n++)
            {
                int i = _liveList[n];
                if (!_live[i] || !TargetGeometry.SegmentSphere(from, to, _aim[i], body, out float t) || t >= best)
                    continue;
                best = t;
                index = i;
            }

            if (index < 0)
                return false;
            version = _versions[index];
            point = Vector3.Lerp(from, to, best);
            return true;
        }

        public void Damage(int index, int version, float damage, Vector3 hitPoint)
        {
            if (damage <= 0f || !IsAlive(index, version))
                return;

            _dealt[index] += damage;
            if (_dealt[index] < _health[index])
                return;

            // The kill must land now (the bullet is spent on it): a running step is waited for.
            if (_simulation.IsRunning)
                _horde.CompleteFrame();

            // The snapshot may be a frame old: the monster may have died since, and a new one may hold its slot.
            if (!ConfirmSlot(index))
                return;

            _simulation.Kill(index);
            MarkDead(index);
        }

        /// <summary>
        /// True when the slot still holds the monster of the snapshot (read now, with no step running). Dead: marked
        /// so. Taken by a new monster (more health than the snapshot had, or somewhere else): the slot's snapshot is
        /// refreshed with a new version, and the damage dealt to the old one forgotten.
        /// </summary>
        private bool ConfirmSlot(int i)
        {
            if (_simulation.IsRunning)
                return false;

            HordeState state = _simulation.GetState(i);
            if (state != HordeState.Walk && state != HordeState.Attack)
            {
                MarkDead(i);
                return false;
            }

            Vector3 aim = (Vector3)_simulation.GetPosition(i) + Vector3.up * AIM_HEIGHT;
            float health = _simulation.GetHealth(i);
            if (health <= _health[i] + HEALTH_EPSILON && (aim - _aim[i]).sqrMagnitude <= REUSED_JUMP_SQ)
            {
                _aim[i] = aim;
                return true;
            }

            _versions[i]++;
            _dealt[i] = 0f;
            _aim[i] = aim;
            _health[i] = health;
            return false;
        }

        public void DamageArea(Vector3 center, float radius, float damage, float minFactor)
        {
            if (radius > 0f && damage > 0f)
                _simulation.AddAreaDamage(center, radius, damage);
        }

        public int CountInRadius(Vector3 center, float radius)
        {
            Refresh();
            float sq = radius * radius;
            int count = 0;
            for (int n = 0; n < _liveCount; n++)
            {
                int i = _liveList[n];
                if (_live[i] && (_aim[i] - center).sqrMagnitude <= sq)
                    count++;
            }

            return count;
        }

        private void MarkDead(int i)
        {
            if (!_live[i])
                return;
            _live[i] = false;
            _versions[i]++;
            _dealt[i] = 0f;
        }

        /// <summary>A new snapshot, once a frame, only while no step runs.</summary>
        private void Refresh()
        {
            int frame = Time.frameCount;
            if (frame == _snapshotFrame || _simulation.IsRunning)
                return;

            using (SnapshotMarker.Auto())
            {
                _snapshotFrame = frame;
                _peak = math.max(_peak, _simulation.AliveCount);
                int end = math.min(_simulation.Capacity, _peak + SCAN_MARGIN);
                _liveCount = 0;

                for (int i = 0; i < end; i++)
                {
                    HordeState state = _simulation.GetState(i);
                    bool live = state == HordeState.Walk || state == HordeState.Attack;
                    if (live != _live[i])
                    {
                        _live[i] = live;
                        _versions[i]++;
                        _dealt[i] = 0f;
                    }

                    if (!live)
                        continue;

                    _aim[i] = (Vector3)_simulation.GetPosition(i) + Vector3.up * AIM_HEIGHT;
                    _health[i] = _simulation.GetHealth(i);
                    _liveList[_liveCount++] = i;
                }
            }
        }
    }
}
