using System;

namespace Saving
{
    /// <summary>Short ways to save plain code without a serializer class.</summary>
    public static class SaveLoadServiceExtensions
    {
        /// <summary>
        /// Saves a value through a getter and a setter: saves.Register("volume", () => volume, v => volume = v).
        /// Returns the serializer, which Unregister takes too.
        /// </summary>
        public static DelegateSerializer<T> Register<T>(this ISaveLoadService saves, string key, Func<T> get, Action<T> set,
            int order = 0)
        {
            if (saves == null)
                throw new ArgumentNullException(nameof(saves));

            var serializer = new DelegateSerializer<T>(key, get, set, order);
            saves.Register(serializer);
            return serializer;
        }
    }
}
