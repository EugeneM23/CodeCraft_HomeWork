namespace Saving
{
    public enum LoadOutcome
    {
        Loaded,
        Recovered,        // save.json was broken or missing, its .bak was loaded
        Empty,            // no save: a new game
        Corrupted,        // both files broken: renamed to *.corrupt, a new game
        NewerVersion,     // written by a newer game: nothing applied, slot read-only
        MigrationFailed,  // a migration threw: nothing applied, slot read-only
        Failed,           // the storage threw: nothing applied, slot read-only
        SceneNotFound     // the saved scene is not in the build: nothing applied, slot read-only
    }
}
