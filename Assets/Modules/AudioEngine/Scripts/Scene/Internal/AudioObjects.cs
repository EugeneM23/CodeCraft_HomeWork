using UnityEngine;

namespace AudioEngine
{
    /// <summary>
    /// Destroys pooled objects the way the current mode allows: Destroy in Play, DestroyImmediate in edit mode
    /// (edit-mode tests and tools), where Destroy only logs an error and leaks the object.
    /// </summary>
    internal static class AudioObjects
    {
        internal static void Destroy(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
