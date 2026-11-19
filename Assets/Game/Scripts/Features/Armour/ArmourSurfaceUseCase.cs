using UnityEngine;

namespace Game
{
    /// <summary>
    /// The real slope of a voxel plate where a shell struck it. One voxel's face only knows six directions, so on a
    /// sloped plate it says «flat» or «vertical» by the stair it hit; a ring of rays parallel to the shot round the
    /// hit samples the plate a few voxels about and fits a plane through what they struck.
    /// </summary>
    public static class ArmourSurfaceUseCase
    {
        public const int RING = 8;

        /// <summary>How far out (m) the ring samples the plate: a few voxels, to smooth their stairs.</summary>
        public const float RADIUS = 0.15f;

        private static readonly RaycastHit[] s_hits = new RaycastHit[16];
        private static readonly Vector3[] s_points = new Vector3[RING];
        private static readonly bool[] s_found = new bool[RING];

        /// <summary>
        /// The plate's outward normal at <paramref name="hit"/>, struck along <paramref name="direction"/>: a plane
        /// fitted through the points the ring of rays struck on the same body (or collider when there is no body);
        /// the hit's own normal when too few of them did.
        /// </summary>
        public static Vector3 SmoothNormal(RaycastHit hit, Vector3 direction, float radius = RADIUS, int mask = ~0)
        {
            Vector3 dir = direction.normalized;
            Transform owner = Owner(hit.collider);
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(side, dir);
            Vector3 centre = hit.point;
            int found = 0;
            for (int i = 0; i < RING; i++)
            {
                float a = i * (Mathf.PI * 2f / RING);
                Vector3 offset = (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius;
                Vector3 from = centre + offset - dir * (radius * 3f);
                s_found[i] = false;
                int count = Physics.RaycastNonAlloc(from, dir, s_hits, radius * 6f, mask, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                for (int h = 0; h < count; h++)
                {
                    if (s_hits[h].distance >= best || Owner(s_hits[h].collider) != owner)
                        continue;
                    best = s_hits[h].distance;
                    s_points[i] = s_hits[h].point;
                    s_found[i] = true;
                }

                if (s_found[i])
                    found++;
            }

            if (found < 3)
                return hit.normal;

            // Sum of the fan's triangle normals round the centre: an area-weighted plane through the samples.
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < RING; i++)
            {
                int j = (i + 1) % RING;
                if (!s_found[i] || !s_found[j])
                    continue;
                sum += Vector3.Cross(s_points[i] - centre, s_points[j] - centre);
            }

            // Opposite samples where neighbours are missing still span the plane.
            if (sum.sqrMagnitude < 1e-10f)
                return hit.normal;
            Vector3 normal = sum.normalized;
            if (Vector3.Dot(normal, dir) > 0f)
                normal = -normal;
            return normal;
        }

        /// <summary>What a sample must strike to count: the same voxel body, or the same collider.</summary>
        private static Transform Owner(Collider collider)
        {
            if (collider == null)
                return null;
            var body = collider.GetComponentInParent<Voxels.VoxelBody>();
            return body != null ? body.transform : collider.transform;
        }
    }
}
