using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game
{
    /// <summary>
    /// Entity enemies (the GameContext's EnemyFilter: Regax and the elites) as a target source. Every enemy gets a
    /// slot of a preallocated table when the filter adds it and loses it when the filter drops it; the slot's version
    /// grows on every change, so an old <see cref="EnemyRef"/> never hits the next tenant. Damage goes through
    /// <see cref="DamageUseCase"/> from one reusable hitter entity that carries the damage (as TankCrushBehaviour's
    /// crusher does); area damage through a NonAlloc overlap on the Enemy layer.
    /// <para>
    /// TEMPORARY (until the AI team reports entity deaths itself): reports every entity enemy's death to the level's
    /// <see cref="IEnemyDeaths"/> — <see cref="EnemyKind.EliteRegax"/> for a <c>Regax_Elite*</c>, else
    /// <see cref="EnemyKind.Crawler"/>. Turn it off with the constructor's <c>reportDeaths</c>.
    /// </para>
    /// </summary>
    public sealed class EntityEnemySource : IPhysicalEnemyTargetSource, IEnemyTargetRingQuery, IDisposable
    {
        private const int CAPACITY = 256;
        private const int OVERLAP_CAPACITY = 128;
        private const string ELITE_PREFIX = "Regax_Elite";

        /// <summary>Aim point above the feet when an enemy has no collider.</summary>
        private const float FALLBACK_AIM_HEIGHT = 1f;

        /// <summary>Body radius when an enemy has no collider.</summary>
        private const float FALLBACK_RADIUS = 0.5f;

        /// <summary>TankCrushBehaviour's crusher deals this much: a killing blow at least this big is a crush.</summary>
        private const int CRUSH_DAMAGE_MIN = 100000;

        private static readonly Collider[] Overlap = new Collider[OVERLAP_CAPACITY];
        private static readonly IEntity[] Visited = new IEntity[OVERLAP_CAPACITY];

        private readonly GameContext _context;
        private readonly EntityFilter _filter;
        private readonly bool _reportDeaths;

        private readonly IEntity[] _entities = new IEntity[CAPACITY];
        private readonly Transform[] _transforms = new Transform[CAPACITY];
        private readonly Collider[] _colliders = new Collider[CAPACITY];
        private readonly int[] _versions = new int[CAPACITY];
        private int _count;

        private readonly Action<IEntity> _onAdded;
        private readonly Action<IEntity> _onRemoved;
        private readonly Action<TakeDamageArgs> _onDamaged;
        private readonly Action<TakeDamageArgs> _onDied;

        private readonly GameObject _hitterObject;
        private readonly IEntity _hitter;
        private readonly IReactiveVariable<int> _hitterDamage;
        private readonly TakeDamageArgs _hitArgs;

        /// <summary>The source of the last hit any enemy took: the killing blow by the time its death is raised.</summary>
        private IEntity _lastHitSource;
        private float _lastHitFactor;

        public EntityEnemySource(GameContext context, EntityFilter filter, bool reportDeaths)
        {
            _context = context;
            _filter = filter;
            _reportDeaths = reportDeaths;

            _onAdded = OnAdded;
            _onRemoved = OnRemoved;
            _onDamaged = OnDamaged;
            _onDied = OnDied;

            // Bullet blood (TakeDamageBloodBehaviour) reads where the hit came from: the hitter stands at the hit point.
            _hitterObject = new GameObject("[EnemyHitter]") { hideFlags = HideFlags.HideAndDontSave };
            _hitter = new Entity();
            _hitter.AddRangeWeaponTag();
            _hitter.AddTransform(_hitterObject.transform);
            _hitterDamage = new ReactiveInt(0);
            _hitter.AddDamage(_hitterDamage);
            _hitArgs = new TakeDamageArgs(_hitter);

            if (_filter == null)
                return;

            foreach (IEntity entity in _filter)
                OnAdded(entity);
            _filter.OnAdded += _onAdded;
            _filter.OnRemoved += _onRemoved;
        }

        public void Dispose()
        {
            if (_filter != null)
            {
                _filter.OnAdded -= _onAdded;
                _filter.OnRemoved -= _onRemoved;
            }

            for (int i = 0; i < CAPACITY; i++)
                if (_entities[i] != null)
                    Release(i);

            if (_hitterObject != null)
                Object.Destroy(_hitterObject);
        }

        public int AliveCount => _count;

        public bool TryFindNearest(Vector3 from, float range, out int index, out int version, out Vector3 position) =>
            TryFindNearestBeyond(from, -1f, range, out index, out version, out position);

        public bool TryFindNearestBeyond(Vector3 from, float minRange, float maxRange, out int index, out int version,
            out Vector3 position)
        {
            index = -1;
            version = 0;
            position = default;
            if (_count == 0)
                return false;

            float bestSq = maxRange * maxRange;
            float minSq = minRange > 0f ? minRange * minRange : -1f;
            for (int i = 0; i < CAPACITY; i++)
            {
                if (!IsLive(i))
                    continue;

                Vector3 at = AimPoint(i);
                float sq = (at - from).sqrMagnitude;
                if (sq > bestSq || sq <= minSq)
                    continue;

                bestSq = sq;
                index = i;
                position = at;
            }

            if (index < 0)
                return false;
            version = _versions[index];
            return true;
        }

        public bool IsAlive(int index, int version) =>
            index >= 0 && index < CAPACITY && _versions[index] == version && IsLive(index);

        public Vector3 GetPosition(int index) =>
            index >= 0 && index < CAPACITY && _entities[index] != null ? AimPoint(index) : default;

        public bool Raycast(Vector3 from, Vector3 to, float radius, out int index, out int version, out Vector3 point)
        {
            index = -1;
            version = 0;
            point = to;
            float best = float.MaxValue;

            for (int i = 0; i < CAPACITY; i++)
            {
                if (!IsLive(i))
                    continue;
                if (!TargetGeometry.SegmentSphere(from, to, AimPoint(i), BodyRadius(i) + radius, out float t) ||
                    t >= best)
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
            if (!IsAlive(index, version))
                return;
            Hit(_entities[index], damage, 1f, hitPoint);
        }

        public void DamageArea(Vector3 center, float radius, float damage, float minFactor)
        {
            if (_count == 0 || radius <= 0f || damage <= 0f)
                return;

            int hits = Physics.OverlapSphereNonAlloc(center, radius, Overlap, 1 << PhysicsLayers.Enemy,
                QueryTriggerInteraction.Ignore);
            int visited = 0;
            for (int i = 0; i < hits; i++)
            {
                Collider collider = Overlap[i];
                IEntity target = BlastAreaUseCase.FindEntity(collider);
                if (target == null || !target.HasEnemyTag() || !target.HasDamageableTag() ||
                    Array.IndexOf(Visited, target, 0, visited) >= 0)
                    continue;

                Visited[visited++] = target;
                float distance = Vector3.Distance(center, collider.bounds.ClosestPoint(center));
                Hit(target, damage, TargetGeometry.Falloff(distance, radius, minFactor), center);
            }

            Array.Clear(Visited, 0, visited);
            Array.Clear(Overlap, 0, hits);
        }

        public int CountInRadius(Vector3 center, float radius)
        {
            float sq = radius * radius;
            int count = 0;
            for (int i = 0; i < CAPACITY; i++)
                if (IsLive(i) && (AimPoint(i) - center).sqrMagnitude <= sq)
                    count++;
            return count;
        }

        // ------------------------------------------------------------------ slots

        private bool IsLive(int i)
        {
            IEntity entity = _entities[i];
            return entity != null && entity.TryGetHealth(out Health health) && health.Exists();
        }

        private Vector3 AimPoint(int i)
        {
            Collider collider = _colliders[i];
            if (collider != null && collider.enabled)
                return collider.bounds.center;
            Transform transform = _transforms[i];
            return transform != null ? transform.position + Vector3.up * FALLBACK_AIM_HEIGHT : default;
        }

        private float BodyRadius(int i)
        {
            Collider collider = _colliders[i];
            if (collider == null)
                return FALLBACK_RADIUS;
            Vector3 extents = collider.bounds.extents;
            return Mathf.Max(extents.x, extents.z);
        }

        private void Hit(IEntity target, float damage, float factor, Vector3 hitPoint)
        {
            // DamageUseCase reads the whole damage from the source and scales it by the factor.
            _hitterDamage.Value = Mathf.Max(1, Mathf.RoundToInt(damage));
            _hitArgs.source = _hitter;
            _hitArgs.damageFactor = factor;
            _hitterObject.transform.position = hitPoint;
            DamageUseCase.TakeDamage(target, _hitArgs);
        }

        // The filter's events: an enemy appears or is gone (allocations allowed: not a tick).
        private void OnAdded(IEntity entity)
        {
            if (entity == null || Array.IndexOf(_entities, entity) >= 0)
                return;

            int slot = Array.IndexOf(_entities, null);
            if (slot < 0)
            {
                Debug.LogWarning($"[EntityEnemySource] More than {CAPACITY} entity enemies: the rest are not targeted.");
                return;
            }

            _entities[slot] = entity;
            _versions[slot]++;
            _transforms[slot] = entity.TryGetTransform(out Transform transform) ? transform : null;
            _colliders[slot] = FindBody(entity, transform);
            _count++;

            if (entity.TryGetDamageTakenEvent(out IEvent<TakeDamageArgs> damaged))
                damaged.OnEvent += _onDamaged;
            if (_reportDeaths && entity.TryGetDeathTakenEvent(out IEvent<TakeDamageArgs> died))
                died.OnEvent += _onDied;
        }

        private void OnRemoved(IEntity entity)
        {
            int slot = Array.IndexOf(_entities, entity);
            if (slot >= 0)
                Release(slot);
        }

        private void Release(int slot)
        {
            IEntity entity = _entities[slot];
            // A dead enemy is disposed before the world drops it: its events are already gone with its values.
            if (entity.TryGetDamageTakenEvent(out IEvent<TakeDamageArgs> damaged))
                damaged.OnEvent -= _onDamaged;
            if (entity.TryGetDeathTakenEvent(out IEvent<TakeDamageArgs> died))
                died.OnEvent -= _onDied;

            _entities[slot] = null;
            _transforms[slot] = null;
            _colliders[slot] = null;
            _versions[slot]++;
            _count--;
        }

        private static Collider FindBody(IEntity entity, Transform transform)
        {
            if (transform == null)
                return null;
            if (transform.TryGetComponent(out CapsuleCollider capsule))
                return capsule;
            Collider collider = transform.GetComponentInChildren<Collider>();
            return collider != null && !collider.isTrigger ? collider : null;
        }

        private void OnDamaged(TakeDamageArgs args)
        {
            _lastHitSource = args?.source;
            _lastHitFactor = args != null ? args.damageFactor : 1f;
        }

        // DeathBehaviour raises the death with the dead entity as the source, right after the killing blow's
        // DamageTakenEvent (DamageUseCase raises that from inside Health.Reduce).
        private void OnDied(TakeDamageArgs args)
        {
            IEntity dead = args?.source;
            if (dead == null || !_context.TryGetEnemyDeaths(out IEnemyDeaths deaths) || deaths == null)
                return;

            bool elite = IsElite(dead);
            bool crushed = IsCrush(_lastHitSource);
            Vector3 point = dead.TryGetTransform(out Transform transform) && transform != null
                ? transform.position
                : default;

            _lastHitSource = null;
            deaths.Report(new EnemyDeath(elite ? EnemyKind.EliteRegax : EnemyKind.Crawler, point, crushed, elite));
        }

        private bool IsCrush(IEntity source) =>
            source != null && !source.HasRangeWeaponTag() && _lastHitFactor >= 1f &&
            source.TryGetDamage(out IReactiveVariable<int> damage) && damage.Value >= CRUSH_DAMAGE_MIN;

        private static bool IsElite(IEntity entity)
        {
            string name = entity.Name;
            if (!string.IsNullOrEmpty(name) && name.StartsWith(ELITE_PREFIX, StringComparison.Ordinal))
                return true;
            return entity.TryGetGameObject(out GameObject go) && go != null &&
                   go.name.StartsWith(ELITE_PREFIX, StringComparison.Ordinal);
        }
    }
}
