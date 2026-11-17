using System.Collections;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// STUB helper of SceneGameFlow: a hidden object that survives scene loads and runs the flow's load coroutines.
    /// Created on first use; Play exit destroys it with the other DontDestroyOnLoad objects.
    /// </summary>
    public sealed class SceneGameFlowRunner : MonoBehaviour
    {
        private static SceneGameFlowRunner s_instance;

        public static void Run(IEnumerator routine)
        {
            if (s_instance == null)
            {
                GameObject host = new(nameof(SceneGameFlowRunner)) { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(host);
                s_instance = host.AddComponent<SceneGameFlowRunner>();
            }

            s_instance.StartCoroutine(routine);
        }
    }
}
