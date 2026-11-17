using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A projectile to let go (step 90: one flight for every move — an arrow, a thrown rock, a grenade): who lets it go,
    /// at whom or at which point, how hard a hit on the target is, the numbers of the flight (the shot), and what is
    /// told when it comes down. Straight (Arc 0) — it flies like an arrow and hits what it passes near; a lob (Arc above
    /// 0) — it comes down where the target stood when it was let go (or at Point), and hits the target if it is still
    /// within the shot's hit radius there. A model taken from the hand (Carried — a rock) flies itself instead of the
    /// shot's projectile.
    /// </summary>
    public readonly struct ProjectileLaunch
    {
        /// <summary>Who lets it go: the hit says it came from this one.</summary>
        public readonly Transform Shooter;

        /// <summary>Whom it flies at; null — at Point.</summary>
        public readonly Transform Target;

        /// <summary>Where it flies when there is no Target (a lob at a place on the ground).</summary>
        public readonly Vector3 Point;

        /// <summary>What a hit on the Target deals; 0 — the flight hits nobody itself (the landing does the rest).</summary>
        public readonly float Damage;

        /// <summary>The numbers of the flight: speed, where it leaves from, where on the target it aims, the hit radius.</summary>
        public readonly WeaponShot Shot;

        /// <summary>The shooter's sight probe: walls on the way (null — every line is clear).</summary>
        public readonly ISightProbe Sight;

        /// <summary>0 — straight; above 0 — a lob that rises this share of its length.</summary>
        public readonly float Arc;

        /// <summary>Seconds in the air; 0 — as long as the distance takes at the shot's speed.</summary>
        public readonly float Flight;

        /// <summary>
        /// The model from the hand that flies itself (a rock) — let go already: out of the hand when the launch is made,
        /// the flight's from then on (a launcher may start it later); null — the shot's projectile.
        /// </summary>
        public readonly Transform Carried;

        /// <summary>A weapon on a stand (a turret): the projectile leaves from this model; null — from the shooter's body.</summary>
        public readonly Transform Origin;

        /// <summary>Told once where the projectile came down (or stopped); null — nobody asks.</summary>
        public readonly IProjectileLanding Landing;

        /// <summary>What the one told of the landing gave the launch, to know which of its throws came down.</summary>
        public readonly int Token;

        /// <summary>A shot of a weapon at a target: straight or a lob by the shot's own Arc.</summary>
        public ProjectileLaunch(Transform shooter, Transform target, float damage, WeaponShot shot, ISightProbe sight)
            : this(shooter, target, target != null ? target.position : Vector3.zero, damage, shot, sight,
                shot != null ? shot.Arc : 0f, 0f, null, null, null, 0)
        {
        }

        public ProjectileLaunch(Transform shooter, Transform target, Vector3 point, float damage, WeaponShot shot,
            ISightProbe sight, float arc, float flight, Transform carried, Transform origin, IProjectileLanding landing,
            int token)
        {
            Shooter = shooter;
            Target = target;
            Point = point;
            Damage = damage;
            Shot = shot;
            Sight = sight;
            Arc = Mathf.Max(0f, arc);
            Flight = Mathf.Max(0f, flight);
            Carried = carried;
            Origin = origin;
            Landing = landing;
            Token = token;
        }

        /// <summary>A lob, not a straight flight.</summary>
        public bool IsArc => Arc > 0f;

        /// <summary>Where it comes down when nothing is in the way: the target's feet (or the point).</summary>
        public Vector3 AimPoint => Target != null ? Target.position : Point;
    }

    /// <summary>
    /// Told where a projectile came down (step 90): an ability's effects happen there (Area Damage), its ground mark ends
    /// there. Called once per launch, by the flight or — with no launcher — at once.
    /// </summary>
    public interface IProjectileLanding
    {
        /// <param name="launch">The launch as it was let go.</param>
        /// <param name="point">Where it came down or stopped.</param>
        /// <param name="struck">The target it hit; null — it hit nobody.</param>
        void Landed(in ProjectileLaunch launch, Vector3 point, Transform struck);
    }

    /// <summary>
    /// The body's way to let a projectile fly (the 3D adapter: an arrow that flies straight and hits what it passes near,
    /// or a lob that comes down where the target stood). The core has no engine time and no physics: the flight is the
    /// body's. A body with none — the projectile lands at once (Projectiles.LandAtOnce).
    /// </summary>
    public interface IProjectileLauncher
    {
        void Launch(in ProjectileLaunch launch);

        /// <summary>
        /// The projectile would reach the target if it left now — nothing in the way of its own flight, from where it
        /// leaves (the hands, not the eyes) to where it flies to, with room on each side (Shot.Clearance); a lob over its
        /// arc, not along the straight line. A shot that would hit a wall is not let go. A launcher that cannot tell —
        /// always.
        /// </summary>
        bool CanFly(in ProjectileLaunch launch) => true;
    }

    /// <summary>What the core does with a projectile when the body has no launcher.</summary>
    public static class Projectiles
    {
        /// <summary>
        /// Lets the projectile go through the body's launcher, or — the body has none — lands it at once. A thing of the
        /// world that flies (a thrown rock) says so: nobody may take it until it lands (a search does not run for it).
        /// </summary>
        public static void Launch(IProjectileLauncher launcher, in ProjectileLaunch launch)
        {
            if (launch.Carried != null && launch.Carried.TryGetComponent(out PickableItem thing))
                thing.IsFlying = true;

            if (launcher != null)
                launcher.Launch(launch);
            else
                LandAtOnce(launch);
        }

        /// <summary>
        /// The projectile lands at once (a body that does not fly projectiles): the target is hit where it stands, a
        /// model from the hand is used up, and the landing is told the target's place (or the point).
        /// </summary>
        public static void LandAtOnce(in ProjectileLaunch launch)
        {
            Transform target = launch.Target;
            if (target != null && launch.Damage > 0f)
                DirectStriker.Deliver(launch.Shooter, target, launch.Damage);

            UseUp(launch.Carried);
            launch.Landing?.Landed(launch, launch.AimPoint, target);
        }

        /// <summary>A model that flew is gone once it lands (a thrown rock does not lie about to be picked up again).</summary>
        public static void UseUp(Transform model)
        {
            if (model == null)
                return;

            model.SetParent(null, true);
            if (Application.isPlaying)
                Object.Destroy(model.gameObject);
            else
                Object.DestroyImmediate(model.gameObject);
        }
    }
}
