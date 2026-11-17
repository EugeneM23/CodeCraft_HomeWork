namespace Saving
{
    public enum SyncOutcome
    {
        UpToDate,       // both sides hold the same slot
        Uploaded,       // the local slot went to the server
        Downloaded,     // the server's slot replaced the local one: Load it
        Deleted,        // the server's copy is gone
        NothingToSync,  // neither side has the slot
        Failed          // the server or the package failed: nothing local changed
    }
}
