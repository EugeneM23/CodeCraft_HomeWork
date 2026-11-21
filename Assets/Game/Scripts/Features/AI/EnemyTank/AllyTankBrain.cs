using Atomic.Entities;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Prototype (the user): a friendly copy of the player's tank. It keeps a spot behind the player and to one side,
    /// catching up with boost when left far behind, and shoots HE at the nearest enemy — zombies and enemy tanks alike
    /// (IEnemyTargets). An HE blast hurts only enemies, so a near miss does not hurt us; it still never fires with us in
    /// the line. With nobody to shoot it turns its gun the way we face.
    /// </summary>
    public sealed class AllyTankBrain : TankAIBrain
    {
        private const float BEHIND = 9f;        // the spot: this far behind the player, m
        private const float ASIDE = 6f;         // and this far to its side, m
        private const float SETTLED = 4f;       // nearer the spot than this it stands, m
        private const float SLOW = 12f;         // nearer than this it drives at half throttle, m
        private const float CATCH_UP = 28f;     // further than this it boosts, m
        private const float TOO_CLOSE = 5f;     // backs off the player nearer than this, m
        private const float FIRE_RANGE = 45f;   // looks for enemies inside this, m
        private const float MIN_RANGE = 6f;     // does not fire at its own nose, m
        private const float AIM_UP = 1.2f;      // aims this high over an enemy's position, m

        /// <summary>XZ corners of the ground it may drive to (the spawner's map, a margin in).</summary>
        public static Vector2 MapMin { get; set; } = new(-1000f, -1000f);

        public static Vector2 MapMax { get; set; } = new(1000f, 1000f);

        private float _side;
        private LineOfFire _line = LineOfFire.Clear;

        /// <summary>Spawned and alive friendly tanks (for the sandbox's counter).</summary>
        public static int Alive { get; private set; }

        protected override Vector2 FirePause => new(0.6f, 1.4f);

        protected override void Count(int delta) => Alive += delta;

        protected override void Start()
        {
            base.Start();
            // Every other one on the other side: two allies do not fight for one spot.
            _side = Alive % 2 == 1 ? 1f : -1f;
            if (Entity.TryGetWeapon(out Atomic.Elements.IReactiveVariable<IEntity> cannon) && cannon.Value != null)
                AmmoUseCase.GetOrAdd(cannon.Value).Value = ShellKind.HighExplosive;
        }

        protected override void Think()
        {
            Transform player = Player.Transform;
            if (!Player.IsPresent || player == null)
            {
                Hold(Vector3.zero);
                return;
            }

            Follow(player);
            Shoot(player);
        }

        private void Follow(Transform player)
        {
            Vector3 back = Flat(player.forward).normalized;
            Vector3 right = Flat(player.right).normalized;
            Vector3 spot = player.position - back * BEHIND + right * (ASIDE * _side);
            // Kept on the map: at the sandbox's start the player stands at its west edge, the spot behind was over the void.
            spot.x = Mathf.Clamp(spot.x, MapMin.x, MapMax.x);
            spot.z = Mathf.Clamp(spot.z, MapMin.y, MapMax.y);
            Vector3 toSpot = Flat(spot - transform.position);
            float distance = toSpot.magnitude;
            Vector3 toPlayer = Flat(player.position - transform.position);

            if (toPlayer.magnitude < TOO_CLOSE)
                DriveTo(toPlayer, -0.8f); // facing us, in reverse: away
            else if (distance > SETTLED)
                DriveTo(toSpot, distance > SLOW ? 1f : 0.5f, distance > CATCH_UP);
            else
                Hold(back);
        }

        private void Shoot(Transform player)
        {
            // The horde (IEnemyTargets) and the enemy tanks, which that list does not hold: the nearest of them.
            float best = FIRE_RANGE;
            bool found = false;
            Vector3 enemy = default;
            Transform body = null;
            if (GameContext.TryGetInstance(out GameContext context) && context.TryGetEnemyTargets(out IEnemyTargets targets)
                && targets.TryFindNearest(GunPosition, FIRE_RANGE, out EnemyRef _, out Vector3 zombie))
            {
                enemy = zombie;
                best = Vector3.Distance(GunPosition, zombie);
                found = true;
            }
            foreach (EnemyTankBrain tank in EnemyTankBrain.All)
            {
                if (tank == null || !tank.enabled)
                    continue;
                float d = Vector3.Distance(GunPosition, tank.transform.position);
                if (d >= best)
                    continue;
                best = d;
                enemy = tank.transform.position;
                body = tank.transform;
                found = true;
            }

            if (!found)
            {
                AimAlong(Flat(player.forward));
                return;
            }

            Vector3 target = enemy + Vector3.up * AIM_UP;
            AimAt(target);
            if (Flat(enemy - transform.position).magnitude < MIN_RANGE)
                return;

            if (Time.frameCount % 6 == 0)
                _line = Line(target, body, player);
            TryFire(target, _line);
        }
    }
}
