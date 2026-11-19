using UnityEngine;

namespace Game
{
    /// <summary>A straight run of wooden fence along X or Z.</summary>
    public struct FencePlan
    {
        /// <summary>One end of the run, on the ground (world).</summary>
        public Vector3 Start;

        /// <summary>Length (m) along +X, or along +Z when <see cref="AlongZ"/>.</summary>
        public float Length;

        public bool AlongZ;
        public int Seed;
    }
}
