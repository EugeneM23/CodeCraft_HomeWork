namespace Saving
{
    /// <summary>Where SaveLoadSystem keeps slots.</summary>
    public enum SaveStorageKind
    {
        Files,        // persistentDataPath/<folder>/slot_N/
        PlayerPrefs,  // keys <folder>/slot_N/<file>: WebGL, small games
        Custom        // a SaveStorageAsset: a database, a platform API
    }
}
