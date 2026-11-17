using System.Collections.Generic;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// The one door the game uses to break voxels: a blast, a bullet, a ram. Bodies are found by
    /// their bounds, not by physics queries, so the tank's voxel skin (which has no colliders of
    /// its own) is hit just like a wall.
    /// </summary>
    public static class VoxelDamage
    {
        /// <summary>Most debris cubes one blast throws, over all the bodies it touches.</summary>
        public const int BLAST_DEBRIS = 900;

        private static readonly List<VoxelBody> Hit = new();

        /// <summary>Whether a collider is part of a voxel body (a chunk's box).</summary>
        public static bool IsVoxel(Collider collider) =>
            collider != null && collider.GetComponentInParent<VoxelBody>() != null;

        /// <returns>How many voxels were taken out.</returns>
        /// <param name="ignore">Bodies under this transform are spared (a vehicle ramming with its own skin).</param>
        /// <param name="terrain">False: the ground is spared (a ram bites walls, not the earth under them).</param>
        public static int Blast(Vector3 point, float radius, float force, Transform ignore = null, bool terrain = true)
        {
            IReadOnlyList<VoxelBody> bodies = VoxelWorld.Bodies;
            if (bodies.Count == 0 || radius <= 0f)
                return 0;

            Hit.Clear();
            float reach = radius * radius;
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody body = bodies[i];
                if (body == null || !body.IsCreated || body.SolidCount == 0)
                    continue;
                if (ignore != null && body.transform.IsChildOf(ignore))
                    continue;
                if (!terrain && body.IsTerrain)
                    continue;
                if (body.WorldBounds.SqrDistance(point) <= reach)
                    Hit.Add(body);
            }

            if (Hit.Count == 0)
                return 0;

            int budget = BLAST_DEBRIS / Hit.Count;
            int total = 0;
            foreach (VoxelBody body in Hit)
                if (body != null)
                    total += body.CarveSphere(point, radius, force, budget);

            Hit.Clear();
            return total;
        }

        /// <summary>
        /// An air bomb: big lumps of ground are ripped out whole and thrown high and outwards (debris
        /// clumps without colliders: they tumble, land, break up and go), then a crater is dug — a
        /// bowl with no wall at its edge, which a tank drives out of (<see cref="VoxelBody.CarveCrater"/>) — and the rest
        /// of the earth goes up as a fountain of clods. Anything else in reach (a house, a wall) gets
        /// an ordinary crater.
        /// </summary>
        /// <param name="point">Where the bomb struck the ground.</param>
        /// <param name="radius">Radius of the crater's rim.</param>
        /// <param name="slope">Steepest slope (degrees) of the crater's wall.</param>
        /// <param name="force">Speed (m/s) the clods leave the crater with.</param>
        /// <param name="lumps">How many lumps of ground fly off whole.</param>
        /// <param name="lumpSize">Radius (m) of a lump, ±30 %.</param>
        /// <param name="lumpSpeed">Upward speed (m/s) of a lump, ±25 %.</param>
        /// <param name="debris">Most debris cubes the fountain throws.</param>
        /// <param name="life">How long (s, ±25 %) lumps and clods lie before they go.</param>
        /// <returns>How many voxels went (a lump counts its cubes).</returns>
        public static int AirBlast(Vector3 point, float radius, float slope, float force, int lumps, float lumpSize,
            float lumpSpeed, int debris = 2600, float life = 12f)
        {
            IReadOnlyList<VoxelBody> bodies = VoxelWorld.Bodies;
            if (bodies.Count == 0 || radius <= 0f)
                return 0;

            Hit.Clear();
            float reach = radius * radius;
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody body = bodies[i];
                if (body != null && body.IsCreated && body.SolidCount > 0 && body.WorldBounds.SqrDistance(point) <= reach)
                    Hit.Add(body);
            }

            int total = 0;
            foreach (VoxelBody body in Hit)
            {
                if (body == null || !body.IsTerrain)
                    continue;

                // Lumps from round the middle, their tops at the surface: turf on earth. Only from
                // where the bowl is deep enough to take their bottoms, or they would leave pits in
                // its slope.
                for (int i = 0; i < lumps; i++)
                {
                    float size = lumpSize * Random.Range(0.7f, 1.3f);
                    Vector2 flat = Random.insideUnitCircle * (radius * 0.25f);
                    Vector3 at = point + new Vector3(flat.x, -size * 0.35f, flat.y);
                    Vector3 away = new Vector3(flat.x, 0f, flat.y);
                    away = away.sqrMagnitude > 0.01f ? away.normalized : Random.onUnitSphere;
                    away.y = 0f;
                    Vector3 velocity = Vector3.up * (lumpSpeed * Random.Range(0.75f, 1.25f)) +
                                       away.normalized * (lumpSpeed * Random.Range(0.25f, 0.6f));
                    Vector3 spin = Random.insideUnitSphere * Random.Range(2f, 7f);
                    total += body.Tear(at, size, velocity, spin, life);
                }

                total += body.CarveCrater(point, radius, slope, force, debris, life);
            }

            // A house takes a 2 m bite from a 4 m crater, as it did from the old 3.4 m one.
            foreach (VoxelBody body in Hit)
                if (body != null && !body.IsTerrain && body.IsCreated)
                    total += body.CarveSphere(point, radius * 0.5f, force, BLAST_DEBRIS / Mathf.Max(1, Hit.Count));

            Hit.Clear();
            return total;
        }

        /// <summary>
        /// A bullet chips a small bite just under the surface it struck.
        /// </summary>
        public static int Impact(Vector3 point, Vector3 direction, float radius, float force = 2.5f)
        {
            Vector3 inward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            return Blast(point + inward * (radius * 0.5f), radius, force);
        }
    }
}
