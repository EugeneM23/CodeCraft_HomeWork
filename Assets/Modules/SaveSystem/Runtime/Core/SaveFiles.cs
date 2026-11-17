namespace Saving
{
    /// <summary>File names inside a slot folder.</summary>
    public static class SaveFiles
    {
        public const string SAVE = "save.json";
        public const string META = "meta.json";
        public const string PREMIGRATION = "save.premigration.json";
        public const string BACKUP_SUFFIX = ".bak";
        public const string CORRUPT_SUFFIX = ".corrupt";

        /// <summary>The files a slot package carries: an exported slot is these files and nothing else.</summary>
        public static readonly string[] Packaged = { SAVE, META };
    }
}
