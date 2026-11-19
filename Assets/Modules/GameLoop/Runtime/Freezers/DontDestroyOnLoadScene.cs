using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ticking
{
    /// <summary>
    /// The DontDestroyOnLoad scene, which <c>SceneManager</c> does not list: found through a hidden empty object
    /// moved there once. Invalid outside Play mode (nothing can be moved there).
    /// </summary>
    internal static class DontDestroyOnLoadScene
    {
        private static GameObject s_marker;

        public static Scene Get()
        {
            if (!Application.isPlaying)
                return default;

            if (s_marker == null)
            {
                s_marker = new GameObject("[GameLoop] DontDestroyOnLoad marker")
                {
                    hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable
                };
                Object.DontDestroyOnLoad(s_marker);
            }

            return s_marker.scene;
        }
    }
}
