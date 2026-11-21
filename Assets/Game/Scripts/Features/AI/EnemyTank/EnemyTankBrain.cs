using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Prototype (the user): an enemy copy of the player's tank. It closes to a fighting range, holds there turning its
    /// hull to us, backs off when we come too close, and shoots armour-piercing once the turret is on us. Something solid
    /// (not voxels) in the way: it comes on in till there is a line.
    /// </summary>
    public sealed class EnemyTankBrain : TankAIBrain
    {
        private const float FAR = 24f;          // drives up while further than this, m
        private const float NEAR = 12f;         // backs off while nearer than this, m
        private const float FIRE_RANGE = 55f;   // shoots inside this, m

        private LineOfFire _line = LineOfFire.Clear;

        /// <summary>Spawned and alive enemy tanks (for the sandbox's counter).</summary>
        public static int Alive { get; private set; }

        private static readonly System.Collections.Generic.List<EnemyTankBrain> s_all = new();

        /// <summary>The live enemy tanks (the ally's targets: IEnemyTargets holds only the horde).</summary>
        public static System.Collections.Generic.IReadOnlyList<EnemyTankBrain> All => s_all;

        protected override void Count(int delta)
        {
            Alive += delta;
            if (delta > 0)
                s_all.Add(this);
            else
                s_all.Remove(this);
        }

        protected override void Start()
        {
            base.Start();
            Entity.AddEnemyTag();

            // Armour-piercing: an HE blast hurts only monsters (ExplosionUseCase), so HE would never scratch us.
            if (Entity.TryGetWeapon(out IReactiveVariable<IEntity> cannon) && cannon.Value != null)
                AmmoUseCase.GetOrAdd(cannon.Value).Value = ShellKind.ArmourPiercing;
        }

        protected override void Think()
        {
            if (!Player.IsPresent)
            {
                Hold(Vector3.zero);
                return;
            }

            Vector3 target = Player.AimPoint;
            Vector3 flat = Flat(target - transform.position);
            float distance = flat.magnitude;

            AimAt(target);
            if (Time.frameCount % 6 == 0)
                _line = Line(target, Player.Transform);

            if (distance > FAR || (_line == LineOfFire.Blocked && distance > NEAR))
                DriveTo(flat, 1f);
            else if (distance < NEAR)
                DriveTo(flat, -0.7f);
            else
                Hold(flat);

            if (distance <= FIRE_RANGE)
                TryFire(target, _line);
        }
    }
}
