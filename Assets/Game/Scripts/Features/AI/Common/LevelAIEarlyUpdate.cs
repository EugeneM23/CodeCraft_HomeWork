using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;

namespace Game.AI
{
    /// <summary>
    /// A step at the very start of each frame (first in <c>EarlyUpdate</c>, as <c>VoxelWorld</c> does) that completes
    /// the AI jobs scheduled in the last frame — they had the whole render to run on the workers. The hordes' steps come
    /// first: they read the grid's heights on a worker, so the grid's new heights (<see cref="LevelNav.CompleteUpdate"/>)
    /// are written only after them. In the player loop only while a horde or a level grid exists.
    /// </summary>
    public static class LevelAIEarlyUpdate
    {
        private static readonly List<HordeBehaviour> s_hordes = new();
        private static readonly List<LevelNav> s_navs = new();

        public static void Add(HordeBehaviour horde) => Add(s_hordes, horde);

        public static void Remove(HordeBehaviour horde) => Remove(s_hordes, horde);

        public static void Add(LevelNav nav) => Add(s_navs, nav);

        public static void Remove(LevelNav nav) => Remove(s_navs, nav);

        private static void Add<T>(List<T> list, T item)
        {
            if (list.Contains(item))
                return;
            list.Add(item);
            if (s_hordes.Count + s_navs.Count == 1)
                SetHook(true);
        }

        private static void Remove<T>(List<T> list, T item)
        {
            if (!list.Remove(item) || s_hordes.Count + s_navs.Count > 0)
                return;
            SetHook(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_hordes.Clear();
            s_navs.Clear();
            SetHook(false);
        }

        /// <summary>
        /// Takes in every horde's running step now. The steps read the level grid's heights on a worker, so whoever
        /// disposes the grid calls this first (<see cref="LevelNav.Dispose"/>).
        /// </summary>
        public static void CompleteHordes()
        {
            for (int i = 0; i < s_hordes.Count; i++)
                s_hordes[i].CompleteFrame();
        }

        private static void Run()
        {
            CompleteHordes();

            // Paused, the loop ticks nobody: the hordes are drawn from here, standing still (as VoxelWorld draws its debris).
            if (GameTicks.Loop is { IsPaused: true })
                for (int i = 0; i < s_hordes.Count; i++)
                    s_hordes[i].DrawPaused();
            for (int i = 0; i < s_navs.Count; i++)
                s_navs[i].CompleteUpdate();
        }

        /// <summary>Marks our entry in the player loop.</summary>
        private struct LevelAISync
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
                bool had = systems.RemoveAll(s => s.type == typeof(LevelAISync)) > 0;
                if (on)
                    systems.Insert(0, new PlayerLoopSystem { type = typeof(LevelAISync), updateDelegate = Run });
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
