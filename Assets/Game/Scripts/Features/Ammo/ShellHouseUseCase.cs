using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using Unity.Mathematics;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// An armour-piercing shell goes through a house (user, 2026-11-19: «простреливает дом насквозь»): each wall on
    /// its way, outer, inner or a floor, is measured along the shot in the house's voxels and costs penetration and
    /// speed by its thickness. The shell bores a hole through it, with the chips thrown on out of the far side, and is
    /// put just past it to fly on. A load-bearing pier costs several times more, so the shell chips it and stops. When
    /// it is spent, it stops in the wall it struck and bursts there with its small blast, as before.
    /// </summary>
    public static class ShellHouseUseCase
    {
        public enum Step
        {
            /// <summary>Not a house, or a shell that does not go through walls: the usual hit.</summary>
            NotAHouse,

            /// <summary>Through the wall: the shell is past it (at Exit) and flies on.</summary>
            PassedThrough,

            /// <summary>A collider whose voxels are already gone (a hole the colliders have not caught up with), or one behind the shell: let by.</summary>
            Hollow,

            /// <summary>Spent in this wall: its entry hole is punched, the usual hit (the small burst) follows.</summary>
            Stopped
        }

        /// <summary>A wall along the shot.</summary>
        public struct Wall
        {
            /// <summary>Solid along the shot (m).</summary>
            public float Thickness;

            /// <summary>Of it, load-bearing (m).</summary>
            public float Hard;

            /// <summary>Distance along the shot from the struck point to the wall's far face (m).</summary>
            public float Depth;

            /// <summary>Whether the far face was found within <see cref="MAX_DEPTH"/>.</summary>
            public bool Through;

            /// <summary>Whether any solid voxel was found just behind the struck point.</summary>
            public bool Solid;
        }

        /// <summary>Solid deeper than this along the shot (a floor run along, a stack of walls) stops the shell.</summary>
        public const float MAX_DEPTH = 2.5f;

        /// <summary>A load-bearing voxel costs this many times an ordinary one: a 20 cm pier (144 mm) stops a fresh shell.</summary>
        public const float HARD_COST = 6f;

        /// <summary>Below this speed (m/s) the shell stops in the next wall.</summary>
        public const float MIN_SPEED = 30f;

        /// <summary>How fast (m/s) the chips of a hole fly on along the shot.</summary>
        public const float CHIP_FORCE = 16f;

        /// <summary>Most chips one wall's hole throws.</summary>
        public const int DEBRIS = 80;

        /// <summary>Whether a voxel body is a building (it has a skeleton and works out its load), not ground or a tank.</summary>
        public static bool IsHouse(VoxelBody body) => body != null && body.HasStrength && !body.IsTerrain;

        /// <param name="penetration">Penetration left (mm), kept per shell; below 0 — a fresh shot (rolled here).</param>
        /// <param name="exit">The far face of the wall, where the shell flies on from (PassedThrough).</param>
        public static Step Resolve(IEntity bullet, Collider collider, Vector3 point, ref float penetration, out Vector3 exit)
        {
            exit = point;
            if (!AmmoUseCase.IsShell(bullet))
                return Step.NotAHouse;
            ShellAmmo ammo = AmmoUseCase.AmmoOf(bullet);
            if (!ammo.CanPassWalls)
                return Step.NotAHouse;
            VoxelBody body = collider != null ? collider.GetComponentInParent<VoxelBody>() : null;
            if (!IsHouse(body))
                return Step.NotAHouse;

            Vector3 dir = bullet.GetMoveDirection().Value;
            if (dir.sqrMagnitude < 1e-6f)
                return Step.NotAHouse;
            dir.Normalize();

            // Struck behind the shell (another box of a wall it was just put past), or a hole's stale box.
            if (bullet.TryGetTransform(out Transform shell) && Vector3.Dot(point - shell.position, dir) < -0.15f)
                return Step.Hollow;
            Wall wall = Measure(body, point, dir);
            if (!wall.Solid)
                return Step.Hollow;

            if (penetration < 0f)
                penetration = ArmourMath.RollPenetration(ammo.penetration, 0.08f, UnityEngine.Random.Range(-1f, 1f));

            float cost = Cost(wall, ammo);
            float speed = bullet.TryGetMoveSpeed(out IReactiveVariable<float> moveSpeed) ? moveSpeed.Value : ammo.speed;
            float after = speed * Mathf.Max(0f, 1f - ammo.wallSlow * wall.Thickness);
            if (!wall.Through || cost >= penetration || after < MIN_SPEED)
            {
                // Spent: the hole where it went in, a little wider than the burst's own; the burst follows.
                if (Application.isPlaying)
                    VoxelDamage.Punch(point, dir, ammo.houseHole, CHIP_FORCE * 0.5f, DEBRIS / 2, body);
                return Step.Stopped;
            }

            penetration -= cost;
            if (moveSpeed != null)
                moveSpeed.Value = after;
            Bore(body, point, dir, wall.Depth, ammo.houseHole);
            exit = point + dir * wall.Depth;
            return Step.PassedThrough;
        }

        /// <summary>Penetration (mm) a wall takes: by its thickness, the load-bearing part <see cref="HARD_COST"/> times over.</summary>
        public static float Cost(in Wall wall, in ShellAmmo ammo) =>
            ((wall.Thickness - wall.Hard) + wall.Hard * HARD_COST) * ammo.wallCost;

        /// <summary>
        /// The wall along the shot from where it was struck: its voxels sampled every half voxel until a voxel-wide gap
        /// (the far face) or <see cref="MAX_DEPTH"/>.
        /// </summary>
        public static Wall Measure(VoxelBody body, Vector3 point, Vector3 dir)
        {
            var wall = new Wall();
            float voxel = body.VoxelSize * Mathf.Abs(body.transform.lossyScale.x);
            float step = voxel * 0.5f;
            float lastSolid = -1f, gap = 0f;
            for (float s = step * 0.5f; s <= MAX_DEPTH; s += step)
            {
                byte material = Sample(body, point + dir * s);
                if (material != 0)
                {
                    wall.Solid = true;
                    wall.Thickness += step;
                    if (body.IsHard(material))
                        wall.Hard += step;
                    lastSolid = s;
                    gap = 0f;
                    continue;
                }

                if (!wall.Solid)
                {
                    // Nothing just behind the struck face: a box whose voxels are gone already.
                    if (s > voxel * 1.5f)
                        return wall;
                    continue;
                }

                gap += step;
                if (gap >= voxel)
                {
                    wall.Through = true;
                    wall.Depth = lastSolid + step * 0.5f;
                    return wall;
                }
            }

            wall.Depth = MAX_DEPTH;
            return wall;
        }

        /// <summary>The voxel at a world point (0 — empty or outside the body).</summary>
        private static byte Sample(VoxelBody body, Vector3 world)
        {
            float3 local = body.transform.InverseTransformPoint(world);
            var p = (int3)math.floor((local - body.Origin) / body.VoxelSize);
            return body.VoxelAt(p);
        }

        /// <summary>
        /// A round hole through the wall: bites along the shot, each centred just short of what is left of it, so the
        /// chips are thrown on along the shot and out of the far side.
        /// </summary>
        private static void Bore(VoxelBody body, Vector3 point, Vector3 dir, float depth, float radius)
        {
            if (!Application.isPlaying)
                return;
            float last = Mathf.Max(0f, depth - radius * 0.4f);
            float stride = radius * 0.6f;
            int bites = 1 + Mathf.CeilToInt(last / stride);
            int debris = Mathf.Max(8, DEBRIS / bites);
            for (int i = 0; i < bites && body != null; i++)
                VoxelDamage.Punch(point + dir * Mathf.Min(i * stride, last), dir, radius, CHIP_FORCE, debris, body);
            VoxelSparks.Emit(point, -dir, dir, 20);
        }
    }
}
