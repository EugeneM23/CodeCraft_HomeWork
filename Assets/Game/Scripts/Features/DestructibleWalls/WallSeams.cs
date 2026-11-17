using RayFire;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Seams between the pieces of a RayFire wall: RayfireConnectivity keeps a piece in the wall
    /// as long as a chain of seams links it to an anchor, and drops whatever a cut sets apart on
    /// its next connectivity check.
    /// </summary>
    public static class WallSeams
    {
        /// <summary>Cuts the seam between two pieces, as RayFire's own stress erosion does.</summary>
        public static void Cut(RFShard a, RFShard b)
        {
            int ab = a.neibShards.IndexOf(b);
            if (ab >= 0)
                a.RemoveNeibAt(ab);

            int ba = b.neibShards.IndexOf(a);
            if (ba >= 0)
                b.RemoveNeibAt(ba);
        }

        /// <summary>
        /// Contact area (m²) of a piece and its <paramref name="neighbour"/>-th neighbour. RayFire
        /// measures it from the faces the two meshes share, except on a wall connected by bounding
        /// boxes, where what it stores is a quarter of the two pieces' summed diagonals, a length.
        /// There the area is taken from how the two boxes overlap: they were grown by the wall's
        /// expand to touch, so they overlap by that much across the seam and by that much more
        /// along it.
        /// </summary>
        public static float ContactArea(RayfireConnectivity wall, RFShard piece, int neighbour)
        {
            if (wall.type != ConnectivityType.ByBoundingBox)
                return piece.nArea[neighbour];

            Bounds a = piece.bnd;
            Bounds b = piece.neibShards[neighbour].bnd;
            Vector3 overlap = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);

            // The thinnest overlap runs across the seam, the other two span it.
            float across = Mathf.Min(overlap.x, Mathf.Min(overlap.y, overlap.z));
            float along = Mathf.Max(overlap.x, Mathf.Max(overlap.y, overlap.z));
            float middle = overlap.x + overlap.y + overlap.z - across - along;

            return Mathf.Max(0f, middle - wall.expand) * Mathf.Max(0f, along - wall.expand);
        }

        /// <summary>The same test RayfireConnectivity uses to clean knocked-out pieces from the wall.</summary>
        public static bool IsStanding(RFShard shard) =>
            shard.neibShards != null
            && shard.tm != null
            && shard.rigid != null
            && shard.rigid.act.cnt != null
            && !shard.rigid.lim.demolished;
    }
}
