using UnityEngine;

namespace AIKit.NavMeshNavigation
{
    /// <summary>
    /// How a 3D body lets a projectile fly (IProjectileLauncher) — one launcher for every move (step 90): an arrow flies
    /// straight (ArrowFlight) from in front of the body towards where the target is at the moment of the shot and hits
    /// what it passes near — step aside and it misses; a wall on the way (the shooter's sight probe) stops it. A lob
    /// (Arc above 0 — a thrown rock, a grenade) flies in an arc (ArcFlight) to where the target stood (or to the point)
    /// and hits the target if it is still near where it comes down. A model taken from the hand (a rock) flies itself,
    /// from where the hand holds it; a gun on a stand shoots from its model. The numbers are the shot's.
    /// </summary>
    public sealed class ArrowLauncher : IProjectileLauncher
    {
        public static readonly ArrowLauncher Shared = new();

        public void Launch(in ProjectileLaunch launch)
        {
            WeaponShot shot = launch.Shot;
            if (launch.Shooter == null || shot == null || (launch.Target == null && !launch.IsArc))
            {
                Projectiles.LandAtOnce(launch);            // nothing to fly at: as a body with no launcher
                return;
            }

            Line(launch, out Vector3 start, out Vector3 aim);
            Vector3 direction = aim - start;
            if (direction.sqrMagnitude < 1e-4f)
                direction = launch.Shooter.forward;

            GameObject flying;
            if (launch.Carried != null)
            {
                flying = launch.Carried.gameObject;
                launch.Carried.SetParent(null, true);     // out of the hand: the flight has it now
            }
            else
            {
                flying = shot.Projectile != null ? Object.Instantiate(shot.Projectile) : Stick();
                flying.name = launch.IsArc ? "Projectile" : "Arrow";
            }

            flying.transform.SetPositionAndRotation(start, Quaternion.LookRotation(direction));
            if (launch.IsArc)
            {
                flying.AddComponent<ArcFlight>().Launch(launch, start);
                return;
            }

            flying.AddComponent<ArrowFlight>().Launch(launch, direction.magnitude + 3f);
        }

        /// <summary>
        /// A straight flight: its own line is free of walls (the shooter's sight probe), with Shot.Clearance of room on
        /// each side of it — two more lines alongside it, all the way. A lob: the two halves of its arc, up to the top and
        /// down onto the target (it passes over low cover a straight line would hit). No sight probe — always.
        /// </summary>
        public bool CanFly(in ProjectileLaunch launch)
        {
            if ((launch.Target == null && !launch.IsArc) || launch.Shooter == null || launch.Shot == null ||
                launch.Sight == null)
                return true;

            Line(launch, out Vector3 start, out Vector3 aim);
            if (launch.IsArc)
            {
                Vector3 top = Top(start, launch.AimPoint, launch.Arc);
                return launch.Sight.HasLineOfSight(start, top) && launch.Sight.HasLineOfSight(top, aim);
            }

            if (!launch.Sight.HasLineOfSight(start, aim))
                return false;

            var flat = new Vector3(aim.x - start.x, 0f, aim.z - start.z);
            float side = launch.Shot.Clearance;
            if (side <= 0.01f || flat.sqrMagnitude < 1e-6f)
                return true;

            Vector3 across = new Vector3(-flat.z, 0f, flat.x).normalized * side;
            return launch.Sight.HasLineOfSight(start + across, aim + across) &&
                   launch.Sight.HasLineOfSight(start - across, aim - across);
        }

        /// <summary>
        /// Where the projectile leaves (in front of the body, toward the target — the clip may hold the body side-on to it
        /// while it draws; a model in the hand — from the hand; a gun on a stand — from its model) and where it flies to
        /// (the target's body, Shot.Aim Height above its feet; a point — the point raised as much).
        /// </summary>
        public static void Line(in ProjectileLaunch launch, out Vector3 start, out Vector3 aim)
        {
            WeaponShot shot = launch.Shot;
            Transform from = launch.Origin != null ? launch.Origin : launch.Shooter;
            aim = launch.AimPoint + Vector3.up * shot.AimHeight;
            Vector3 toward = aim - from.position;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 1e-6f ? toward.normalized : from.forward;
            if (launch.Carried != null)
                start = launch.Carried.position;
            else if (launch.Origin != null)
                start = launch.Origin.position + toward * shot.Forward;
            else
                start = from.position + Vector3.up * shot.Height + toward * shot.Forward;
        }

        /// <summary>The top of a lob from start to where it comes down: half way, raised Arc of its length.</summary>
        public static Vector3 Top(Vector3 start, Vector3 end, float arc) =>
            (start + end) * 0.5f + Vector3.up * (Vector3.Distance(start, end) * arc);

        // A projectile with no model: a thin dark stick
        private static GameObject Stick()
        {
            GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider collider = stick.GetComponent<Collider>();
            if (Application.isPlaying)
                Object.Destroy(collider);
            else
                Object.DestroyImmediate(collider);
            stick.transform.localScale = new Vector3(0.05f, 0.05f, 0.9f);
            // The colour on the renderer only, never on the shared default material
            var colour = new MaterialPropertyBlock();
            colour.SetColor("_Color", new Color(0.25f, 0.18f, 0.1f));
            colour.SetColor("_BaseColor", new Color(0.25f, 0.18f, 0.1f));
            stick.GetComponent<Renderer>().SetPropertyBlock(colour);
            return stick;
        }
    }
}
