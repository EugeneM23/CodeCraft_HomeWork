using UnityEngine;

namespace Game
{
    /// <summary>
    /// Blood for the player's tank to take on its voxel skin (TankBloodBehaviour lays it, a few a physics step):
    /// a splat where death goo hit the hull, and the mess of a body part burst under the tracks. Anyone may add; a
    /// ring of <see cref="CAPACITY"/> where the oldest gives way. World points, taken into the tank's frame when the
    /// tank reads them (the next physics step). Nothing allocated.
    /// </summary>
    public static class TankBloodStains
    {
        public const int CAPACITY = 1024;

        /// <summary>
        /// One spot of blood to lay: a sphere of skin, and whether wet clots go on as well as the stain; or, with
        /// <see cref="Track"/>, a squash under the track on that side: only that track takes it (<see cref="Amount"/>).
        /// </summary>
        public struct Stain
        {
            public Vector3 Point;
            public float Radius;
            public bool Clots;
            public bool Track;
            public float Amount;
        }

        private static readonly Stain[] Ring = new Stain[CAPACITY];
        private static int s_head;
        private static int s_count;

        /// <summary>Stains waiting.</summary>
        public static int Count => s_count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();

        public static void Clear()
        {
            s_head = 0;
            s_count = 0;
        }

        /// <summary>A splat of goo where it hit the hull.</summary>
        public static void Splat(Vector3 point, float radius) => Add(new Stain { Point = point, Radius = radius, Clots = false });

        /// <summary>
        /// A body part burst under a track at <paramref name="ground"/> (where it lay): only that track gets bloody (the
        /// user: the tracks, nothing else), its belt reddens and it leaves bloody prints for a while (VoxelTrackRuts).
        /// <paramref name="heaviness"/> (0..1, the part's size) is how much blood it takes.
        /// </summary>
        public static void Squash(Vector3 ground, float heaviness) =>
            Add(new Stain { Point = ground, Radius = 0.6f, Track = true, Amount = 0.5f + 0.5f * Mathf.Clamp01(heaviness) });

        public static void Add(in Stain stain)
        {
            if (s_count == CAPACITY)
            {
                s_head = (s_head + 1) % CAPACITY;
                s_count--;
            }

            Ring[(s_head + s_count) % CAPACITY] = stain;
            s_count++;
        }

        /// <summary>The oldest stain, taken out. False when none waits.</summary>
        public static bool TryTake(out Stain stain)
        {
            if (s_count == 0)
            {
                stain = default;
                return false;
            }

            stain = Ring[s_head];
            s_head = (s_head + 1) % CAPACITY;
            s_count--;
            return true;
        }
    }
}
