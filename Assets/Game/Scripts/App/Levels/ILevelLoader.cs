using Loading;

namespace Game
{
    /// <summary>
    /// Builds the loading trees of scene changes; runs nothing itself (GameFlow runs the trees). Every call makes a
    /// fresh tree. A level without scenes throws ArgumentException.
    /// </summary>
    public interface ILevelLoader
    {
        /// <summary>One scene Single, replacing everything open (the main menu).</summary>
        LoadingOperation LoadScene(string scenePath);

        /// <summary>The whole level: the first scene Single, the others additively in parallel, then the active one.</summary>
        LoadingOperation Load(LevelConfig level);

        /// <summary>
        /// The editor cold start: some scenes of the level are open already — only the missing ones additively, then
        /// the active scene. The set of missing scenes is taken when the tree is built.
        /// </summary>
        LoadingOperation LoadMissing(LevelConfig level);

        /// <summary>The loaded scenes of the level but the active one, each unloaded on its own.</summary>
        LoadingOperation Unload(LevelConfig level);
    }
}
