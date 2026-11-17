using System;

namespace Saving.Objects
{
    /// <summary>Saving whole objects of plain code through the save service.</summary>
    public static class SaveObjectsExtensions
    {
        /// <summary>
        /// Saves the object under the key until Unregister: its [Save] members, or all its public fields and
        /// properties. Gets its data at once if the slot is already loaded. Returns the serializer.
        /// </summary>
        public static ObjectSerializer Register(this ISaveLoadService saves, string key, object target, int order = 0)
        {
            if (saves == null)
                throw new ArgumentNullException(nameof(saves));

            var serializer = new ObjectSerializer(key, target, order);
            saves.Register(serializer);
            return serializer;
        }
    }
}
