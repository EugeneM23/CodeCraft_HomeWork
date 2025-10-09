using System;
using UnityEngine;

namespace SaveLoadSystem
{
    public class ApplicationEvents : MonoBehaviour
    {
        public event Action OnPaused;
        public event Action OnResumed;
        public event Action OnQuit;

# if UNITY_EDITOR
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                OnResumed?.Invoke();
            else
                OnPaused?.Invoke();
        }
# else
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                OnResumed?.Invoke();
            else
                OnPaused?.Invoke();
        }
#endif
        private void OnApplicationQuit()
        {
            OnQuit?.Invoke();
        }
    }
}