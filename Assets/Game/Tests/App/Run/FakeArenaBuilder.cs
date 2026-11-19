using System;
using System.Collections.Generic;

namespace Game.Tests
{
    /// <summary>Remembers every Build; may call back (e.g. MarkArenaReady) inside Build.</summary>
    public sealed class FakeArenaBuilder : IArenaBuilder
    {
        public readonly List<(ArenaId arena, int seed)> Builds = new();

        /// <summary>Called inside every Build after it is recorded.</summary>
        public Action OnBuild { get; set; }

        /// <summary>Gets a line per Build, for the event order.</summary>
        public List<string> Log { get; set; }

        /// <summary>What Build answers: true — ready at once.</summary>
        public bool ReadyAtOnce { get; set; }

        public bool Build(ArenaId arena, int seed)
        {
            Builds.Add((arena, seed));
            Log?.Add($"Build({arena})");
            OnBuild?.Invoke();
            return ReadyAtOnce;
        }
    }
}
