using System;
using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game
{
    /// <summary>
    /// Fills the pools of what the scene's weapons and vehicles spawn — bullets, casings, impact
    /// effects, the tank's muzzle smoke, blasts and their crumbs — a few instances a frame from just
    /// after the scene starts, so the first shot, hit or blast of each kind rents an instance instead
    /// of making one.
    /// </summary>
    /// <remarks>
    /// GameFactory makes a pool the first time a prefab is spawned and grows it by a synchronous
    /// Instantiate whenever it runs dry, so every new kind of fire hitched during the first seconds
    /// of a fight: an instantiate clones the whole prefab — nine particle systems for the gold
    /// impact — and runs its installers. A pool still grows by itself past what is made here.
    ///
    /// How many: about as many as are alive at once under steady fire — shots a second of the
    /// fastest weapon that spawns the prefab × how long one lives — plus one, capped. Both come off
    /// the prefabs: the rate off each gun's cooldown, the lifetime off the first instance made (its
    /// LifeTime), and so does what an instance spawns in turn (BulletHitPrefab, ExplosionVfxPrefab,
    /// ExplosionCrumbsPrefab), which then gets made too. Retuning a prefab retunes its prewarm.
    ///
    /// Spread over frames by count and by time: never more than <c>perFrame</c> in one, and no more
    /// once a frame has spent <c>frameBudgetMs</c> on them — though always one, so it gets through.
    /// </remarks>
    public sealed class CombatPoolPrewarm : IGameContextInit, IGameContextTick
    {
        /// <summary>Frames left alone after the scene starts: its own first frames are heavy enough.</summary>
        private const int START_DELAY_FRAMES = 2;

        /// <summary>Shots a second taken for a gun without a cooldown: one a frame at 60 fps.</summary>
        private const float UNTIMED_SHOTS_PER_SECOND = 60f;

        /// <summary>Lifetime (s) taken for a prefab that has no LifeTime of its own.</summary>
        private const float DEFAULT_LIFETIME = 1f;

        /// <summary>One prefab's share of the prewarm.</summary>
        private sealed class Entry
        {
            public MonoEntity Prefab;

            /// <summary>How many a second are spawned at most.</summary>
            public float Rate;

            /// <summary>Read off the first instance made; negative until then.</summary>
            public float LifeTime = -1f;

            /// <summary>How many to make; one until the first instance has been read.</summary>
            public int Target = 1;

            public int Made;
        }

        private readonly int _perFrame;
        private readonly double _frameBudget;
        private readonly int _maxPerPrefab;

        private readonly List<Entry> _queue = new();
        private readonly Dictionary<int, Entry> _entries = new();

        private GameFactory _factory;
        private int _delay = START_DELAY_FRAMES;
        private bool _planned;
        private bool _done;
        private int _next;

        /// <param name="perFrame">Most instances made in one frame; 0 turns the prewarm off.</param>
        /// <param name="frameBudgetMs">No more are made in a frame that has spent this long on them.</param>
        /// <param name="maxPerPrefab">Most instances of one prefab made ahead.</param>
        public CombatPoolPrewarm(int perFrame, float frameBudgetMs, int maxPerPrefab)
        {
            _perFrame = Mathf.Max(0, perFrame);
            _frameBudget = Mathf.Max(0f, frameBudgetMs) / 1000.0;
            _maxPerPrefab = Mathf.Max(1, maxPerPrefab);
            _done = _perFrame == 0;
        }

        public void Init(IGameContext context)
        {
            _factory = context.GetGameFactory();
        }

        public void Tick(IGameContext context, float deltaTime)
        {
            if (_done)
                return;

            if (_delay > 0)
            {
                _delay--;
                return;
            }

            // A frame of its own: it walks the scene for weapons and vehicles.
            if (!_planned)
            {
                _planned = true;
                Plan();
                return;
            }

            MakeSome();
        }

        private void Plan()
        {
            // Guns already in the scene: the tank's cannon and machine gun, an enemy's weapon.
            foreach (RangeWeaponInstaller gun in
                     Object.FindObjectsByType<RangeWeaponInstaller>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                WantShotsOf(gun);

            // What a tank spawns besides its guns' rounds: smoke at the muzzle on every shot of the
            // cannon, and the blast it goes up in.
            foreach (TankInstaller tank in
                     Object.FindObjectsByType<TankInstaller>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                RangeWeaponInstaller cannon = tank.Cannon != null
                    ? tank.Cannon.GetComponentInChildren<RangeWeaponInstaller>(true)
                    : null;

                Want(tank.MuzzleSmoke, cannon != null ? ShotsPerSecond(cannon) : 0f);
                Want(tank.ExplosionVfx, 0f);
            }

            _done = _queue.Count == 0;
        }

        private void WantShotsOf(RangeWeaponInstaller gun)
        {
            float rate = ShotsPerSecond(gun);
            Want(gun.BulletPrefab, rate);
            Want(gun.ShellPrefab, rate);
        }

        private static float ShotsPerSecond(RangeWeaponInstaller gun) =>
            gun.FireInterval > 0f ? 1f / gun.FireInterval : UNTIMED_SHOTS_PER_SECOND;

        /// <summary>
        /// Puts <paramref name="prefab"/> in line, spawned <paramref name="rate"/> times a second at
        /// most. Already in line: it keeps the higher rate, and if that raises how many it needs after
        /// it was done, it goes back in line for the rest.
        /// </summary>
        private void Want(MonoEntity prefab, float rate)
        {
            if (prefab == null)
                return;

            int id = prefab.GetInstanceID();
            if (!_entries.TryGetValue(id, out Entry entry))
            {
                entry = new Entry { Prefab = prefab, Rate = rate };
                _entries.Add(id, entry);
                _queue.Add(entry);
                return;
            }

            if (rate <= entry.Rate)
                return;

            entry.Rate = rate;

            // Not read yet: the target follows from the new rate once it is.
            if (entry.LifeTime < 0f)
                return;

            bool finished = entry.Made >= entry.Target;
            entry.Target = Count(entry.Rate, entry.LifeTime);

            if (finished && entry.Made < entry.Target)
                _queue.Add(entry);
        }

        private void MakeSome()
        {
            double stop = Time.realtimeSinceStartupAsDouble + _frameBudget;
            int made = 0;

            while (_next < _queue.Count)
            {
                Entry entry = _queue[_next];
                if (entry.Made >= entry.Target)
                {
                    _next++;
                    continue;
                }

                if (made >= _perFrame || (made > 0 && Time.realtimeSinceStartupAsDouble >= stop))
                    return;

                made++;
                entry.Made++;

                IEntity instance;
                try
                {
                    instance = _factory.Prewarm(entry.Prefab);
                }
                catch (Exception exception)
                {
                    // A prefab that cannot be made now cannot be made on first use either: that is
                    // where it shows. Here it is reported once and skipped, rather than thrown every
                    // frame through the context's tick and everything ticked after it.
                    Debug.LogException(exception, entry.Prefab);
                    entry.Target = entry.Made;
                    continue;
                }

                if (entry.LifeTime < 0f)
                    Read(entry, instance);
            }

            _done = true;
        }

        /// <summary>
        /// The first instance of a prefab: how long one lives, so how many to make, and what it
        /// spawns in turn. A bullet makes one impact, a rocket or a shell one blast and one spray of
        /// crumbs: as often as it is fired.
        /// </summary>
        private void Read(Entry entry, IEntity instance)
        {
            entry.LifeTime = instance != null && instance.TryGetLifeTime(out CooldownTimer life) && life != null
                ? life.Duration
                : DEFAULT_LIFETIME;
            entry.Target = Count(entry.Rate, entry.LifeTime);

            if (instance == null)
                return;

            if (instance.TryGetBulletHitPrefab(out MonoEntity hit))
                Want(hit, entry.Rate);
            if (instance.TryGetExplosionVfxPrefab(out MonoEntity blast))
                Want(blast, entry.Rate);
            if (instance.TryGetExplosionCrumbsPrefab(out MonoEntity crumbs))
                Want(crumbs, entry.Rate);
        }

        /// <summary>Alive at once under steady fire, plus one, capped.</summary>
        private int Count(float rate, float lifeTime)
        {
            int alive = Mathf.CeilToInt(Mathf.Min(rate * lifeTime, _maxPerPrefab));
            return Mathf.Clamp(alive + 1, 1, _maxPerPrefab);
        }
    }
}
