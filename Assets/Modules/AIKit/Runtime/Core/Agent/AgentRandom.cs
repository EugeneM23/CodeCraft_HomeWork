using System;

namespace AIKit
{
    /// <summary>
    /// The agent's own random: a small generator whose whole state is one serializable number, so a save can keep it
    /// and a loaded agent goes on with the very same decisions (handbook §29a: the seed must be saved). SplitMix64 —
    /// fast, no allocation, good enough for choices and jitter. UnityEngine.Random is forbidden in the module
    /// (invariant 2): a bug must replay from the seed.
    /// </summary>
    [Serializable]
    public struct AgentRandom
    {
        /// <summary>The whole state: save it, restore it, the sequence continues.</summary>
        public ulong State;

        public AgentRandom(int seed) => State = Mix((ulong)(uint)seed + 0x9E3779B97F4A7C15UL);

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble() => (Next64() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Uniform integer in [min, max) — max excluded, like System.Random.</summary>
        public int Next(int min, int max)
        {
            if (max <= min)
                return min;

            ulong range = (ulong)((long)max - min);
            return (int)((long)min + (long)(Next64() % range));
        }

        public ulong Next64()
        {
            State += 0x9E3779B97F4A7C15UL;
            return Mix(State);
        }

        /// <summary>A seed for someone else derived from this one (a world handing seeds to its agents).</summary>
        public static int Derive(int seed, int index)
        {
            ulong mixed = Mix(((ulong)(uint)seed << 32) ^ (uint)index ^ 0xD1B54A32D192ED03UL);
            int derived = (int)(mixed ^ (mixed >> 32));
            return derived == 0 ? 1 : derived;              // 0 means "derive", never hand it out
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
