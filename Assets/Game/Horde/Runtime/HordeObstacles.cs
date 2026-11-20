using Unity.Mathematics;
using UnityEngine;

namespace Game.Horde
{
    /// <summary>
    /// Bodies outside the crowd that the crowd keeps out of (the sandbox's skinned zombies): a fixed table of places,
    /// each a feet position and a radius. A body takes a place once (<see cref="Register"/>), moves it every frame
    /// (<see cref="Set"/>) and gives it back when it dies (<see cref="Unregister"/>). The horde copies the table into its
    /// step (<see cref="HordeSimulation.Schedule"/>): monsters step out of these bodies' way, and an attack place on the
    /// hull ring that one of them stands on is taken. Main thread only; no allocations after load.
    /// </summary>
    public static class HordeObstacles
    {
        /// <summary>Places at most (a body that finds none full is not seen by the crowd).</summary>
        public const int CAPACITY = 64;

        private static readonly float4[] s_bodies = new float4[CAPACITY];
        private static readonly bool[] s_used = new bool[CAPACITY];
        private static int s_highWater;

        /// <summary>Places taken now (up to the highest one in use).</summary>
        public static int HighWater => s_highWater;

        /// <summary>A free place, or -1 when the table is full.</summary>
        public static int Register(float3 position, float radius)
        {
            for (int i = 0; i < CAPACITY; i++)
            {
                if (s_used[i])
                    continue;
                s_used[i] = true;
                s_bodies[i] = new float4(position, radius);
                s_highWater = math.max(s_highWater, i + 1);
                return i;
            }

            return -1;
        }

        public static void Set(int handle, float3 position, float radius)
        {
            if (handle >= 0 && handle < CAPACITY && s_used[handle])
                s_bodies[handle] = new float4(position, radius);
        }

        public static void Unregister(int handle)
        {
            if (handle < 0 || handle >= CAPACITY || !s_used[handle])
                return;
            s_used[handle] = false;
            s_bodies[handle] = float4.zero;
            while (s_highWater > 0 && !s_used[s_highWater - 1])
                s_highWater--;
        }

        /// <summary>The body in place <paramref name="i"/>: XYZ the feet, W the radius (0: the place is free).</summary>
        public static float4 Get(int i) => s_bodies[i];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < CAPACITY; i++)
            {
                s_used[i] = false;
                s_bodies[i] = float4.zero;
            }

            s_highWater = 0;
        }
    }
}
