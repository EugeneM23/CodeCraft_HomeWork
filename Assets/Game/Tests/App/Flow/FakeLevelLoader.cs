using System;
using Loading;

namespace Game.Tests
{
    /// <summary>Hands out a ManualOperation for every tree and remembers what was asked.</summary>
    public sealed class FakeLevelLoader : ILevelLoader
    {
        public ManualOperation Last { get; private set; }
        public string LastScene { get; private set; }
        public LevelConfig LastLevel { get; private set; }
        public string LastCall { get; private set; }

        /// <summary>Set — the next tree throws instead (a broken level config).</summary>
        public Exception ThrowOnBuild { get; set; }

        public LoadingOperation LoadScene(string scenePath)
        {
            LastScene = scenePath;
            return Make(nameof(LoadScene), null);
        }

        public LoadingOperation Load(LevelConfig level) => Make(nameof(Load), level);

        public LoadingOperation LoadMissing(LevelConfig level) => Make(nameof(LoadMissing), level);

        public LoadingOperation Unload(LevelConfig level) => Make(nameof(Unload), level);

        private LoadingOperation Make(string call, LevelConfig level)
        {
            if (ThrowOnBuild != null)
                throw ThrowOnBuild;

            LastCall = call;
            LastLevel = level;
            Last = new ManualOperation();
            return Last;
        }
    }
}
