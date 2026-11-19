namespace Game
{
    /// <summary>
    /// The arena team's IArenaService as the run's builder. Looked up in the GameContext at every Build, not at start:
    /// the arena scene may register its service after the run's bootstrap woke. Its Build is synchronous and places the
    /// tank at Entry; the follow camera then jumps to the tank under the closed shutter instead of crossing the map.
    /// No arena service (the sandbox) — nothing to build, the arena counts as ready.
    /// </summary>
    public sealed class ArenaServiceBuilder : IArenaBuilder
    {
        private readonly IGameContext _context;

        public ArenaServiceBuilder(IGameContext context) => _context = context;

        public bool Build(ArenaId arena, int seed)
        {
            if (_context == null || !_context.TryGetArena(out IArenaService arenas) || arenas == null)
                return true;

            bool built = arenas.Build(arena, seed);
            CameraFollowController.RequestSnap();
            return built;
        }
    }
}
