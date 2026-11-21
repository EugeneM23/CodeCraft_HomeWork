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
        private const float FAR = 14f;          // drives up while further than this, m
        private const float NEAR = 7f;          // backs off while nearer than this, m
        private const float FIRE_RANGE = 18f;   // shoots inside this, m (the user: not from far off)
        private const float VIEW_MARGIN = 0.05f; // and only once it is this far inside the screen

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

            // Off screen it comes on in too: it may only shoot from where the player sees it.
            bool seen = OnScreen();
            if (distance > FAR || ((_line == LineOfFire.Blocked || !seen) && distance > NEAR))
                DriveTo(flat, 1f);
            else if (distance < NEAR)
                DriveTo(flat, -0.7f);
            else
                Hold(flat);

            // Only from where the player can see it: a tank off screen does not shoot.
            if (distance <= FIRE_RANGE && seen)
                TryFire(target, _line);
        }

        private static Camera s_camera;

        private bool OnScreen()
        {
            if (s_camera == null)
                s_camera = Camera.main; // perf-ok: looked up again only once the cached camera is gone
            if (s_camera == null)
                return true;
            Vector3 view = s_camera.WorldToViewportPoint(transform.position);
            return view.z > 0f && view.x > VIEW_MARGIN && view.x < 1f - VIEW_MARGIN
                   && view.y > VIEW_MARGIN && view.y < 1f - VIEW_MARGIN;
        }
    }
}
