using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;

namespace Game.AI
{
    /// <summary>
    /// A step at the very start of each frame (first in <c>EarlyUpdate</c>, as <c>VoxelWorld</c>
    /// does) that completes the horde jobs scheduled in the last frame: they had the whole render
    /// to run on the workers. In the player loop only while a horde exists.
    /// </summary>
    public static class HordeEarlyUpdate
    {
        private static readonly List<HordeBehaviour> s_hordes = new();

        public static void Add(HordeBehaviour horde)
        {
            if (s_hordes.Contains(horde))
                return;
            s_hordes.Add(horde);
            if (s_hordes.Count == 1)
                SetHook(true);
        }

        public static void Remove(HordeBehaviour horde)
        {
            if (!s_hordes.Remove(horde) || s_hordes.Count > 0)
                return;
            SetHook(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_hordes.Clear();
            SetHook(false);
        }

        private static void Run()
        {
            for (int i = 0; i < s_hordes.Count; i++)
                s_hordes[i].CompleteFrame();
        }

        /// <summary>Marks our entry in the player loop.</summary>
        private struct HordeSync
        {
        }

        private static void SetHook(bool on)
        {
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            if (loop.subSystemList == null)
                return;

            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.EarlyUpdate))
                    continue;

                PlayerLoopSystem early = loop.subSystemList[i];
                var systems = new List<PlayerLoopSystem>(early.subSystemList ?? System.Array.Empty<PlayerLoopSystem>());
                bool had = systems.RemoveAll(s => s.type == typeof(HordeSync)) > 0;
                if (on)
                    systems.Insert(0, new PlayerLoopSystem { type = typeof(HordeSync), updateDelegate = Run });
                else if (!had)
                    return;

                early.subSystemList = systems.ToArray();
                loop.subSystemList[i] = early;
                PlayerLoop.SetPlayerLoop(loop);
                return;
            }
        }
    }
}
