using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Ticks the app services on Time.unscaledDeltaTime in its own Update. Not on the game loop: pausing the game
    /// must not stop a loading screen or an autosave. On the App prefab next to AppContext.
    /// </summary>
    public sealed class AppTicker : MonoBehaviour
    {
        private readonly List<IAppTickable> _tickables = new();

        public void Add(IAppTickable tickable)
        {
            if (tickable != null && !_tickables.Contains(tickable))
                _tickables.Add(tickable);
        }

        public void Remove(IAppTickable tickable) => _tickables.Remove(tickable);

        private void Update() // perf-ok: ticks during pause (loading, autosave)
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _tickables.Count; i++)
            {
                try
                {
                    _tickables[i].Tick(dt);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
