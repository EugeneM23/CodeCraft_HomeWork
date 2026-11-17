namespace Saving
{
    public enum SaveOutcome
    {
        Saved,
        Unchanged,  // onlyIfChanged and nothing changed: nothing written
        Blocked,    // a load is running or the slot is read-only
        Failed      // the storage threw: the file on disk is the previous one
    }
}
