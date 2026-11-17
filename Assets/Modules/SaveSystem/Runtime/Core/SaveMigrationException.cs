using System;

namespace Saving
{
    /// <summary>A section is older than its serializer and the serializer has no step to upgrade it.</summary>
    public sealed class SaveMigrationException : Exception
    {
        public SaveMigrationException(string key, int fromVersion)
            : base($"'{key}' has no migration from version {fromVersion}.")
        {
            Key = key;
            FromVersion = fromVersion;
        }

        public string Key { get; }
        public int FromVersion { get; }
    }
}
