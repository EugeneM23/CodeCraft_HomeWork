using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ticking
{
    /// <summary>
    /// Base of freezers for a kind of component that Unity runs by itself (Animator, particles, a sprite animator):
    /// on pause every such component of the loaded scenes that <see cref="Freeze"/> stops is remembered, on resume
    /// exactly those get <see cref="Thaw"/>. A scene loaded while the game stands (a scene change behind a black
    /// screen, an additive load) is frozen as it arrives — before its objects run a frame. A new freezer is a
    /// subclass with these two methods.
    /// </summary>
    public abstract class ComponentFreezer<T> : IPauseListener where T : Component
    {
        private readonly List<T> _frozen = new();
        private bool _paused;

        public void OnPause()
        {
            if (_paused)
                return;

            _paused = true;
            foreach (T component in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                TryFreeze(component);

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public void OnResume()
        {
            if (!_paused)
                return;

            _paused = false;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            foreach (T component in _frozen)
            {
                if (component != null)
                    Thaw(component);
            }

            _frozen.Clear();
        }

        /// <summary>Freezes the active components of a scene that came while the game stands; nothing if it runs.</summary>
        public void FreezeScene(Scene scene)
        {
            if (!_paused || !scene.isLoaded)
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>())
                    TryFreeze(component);
            }
        }

        /// <summary>Stops the component; true — it has to be given back on resume.</summary>
        protected abstract bool Freeze(T component);

        /// <summary>Gives back what <see cref="Freeze"/> took.</summary>
        protected abstract void Thaw(T component);

        private void TryFreeze(T component)
        {
            if (!_frozen.Contains(component) && Freeze(component))
                _frozen.Add(component);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => FreezeScene(scene);
    }
}
