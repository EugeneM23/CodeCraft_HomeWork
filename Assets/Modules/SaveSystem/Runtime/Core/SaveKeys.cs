using System;

namespace Saving
{
    public static class SaveKeys
    {
        /// <summary>Section keys starting with this belong to the save system itself.</summary>
        public const string RESERVED_PREFIX = "$";

        public static bool IsReserved(string key) =>
            key != null && key.StartsWith(RESERVED_PREFIX, StringComparison.Ordinal);

        /// <summary>Throws for a key a game section may not use: empty or reserved.</summary>
        public static void ValidateUserKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("The key must not be empty.", nameof(key));

            if (IsReserved(key))
                throw new ArgumentException(
                    $"Keys starting with '{RESERVED_PREFIX}' are reserved for the save system: '{key}'.", nameof(key));
        }
    }
}
