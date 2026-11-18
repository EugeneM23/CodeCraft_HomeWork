using System;
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
    /// <remarks>
    /// Allocation-free after the first pauses: the components are gathered on every pause — so whatever appeared
    /// since the scene started is frozen too — by walking the loaded scenes' roots (and DontDestroyOnLoad's) with
    /// the list overloads of <c>GetRootGameObjects</c> / <c>GetComponentsInChildren</c> into buffers that keep their
    /// capacity, instead of <c>FindObjectsByType</c>, which makes a new array each time. The sceneLoaded subscription
    /// is made once and held (the handler does nothing while running) instead of being added and removed per pause.
    /// Only components of active objects are taken, as <c>FindObjectsByType</c> with inactive objects excluded did.
    /// </remarks>
    public abstract class ComponentFreezer<T> : IPauseListener, IDisposable where T : Component
    {
        private static readonly List<GameObject> s_roots = new(64);

        private readonly List<T> _found = new(64);
        private readonly List<T> _frozen = new(64);
        private readonly HashSet<T> _frozenSet = new();
        private bool _paused;
        private bool _subscribed;
        private bool _disposed;

        public void OnPause()
        {
            if (_paused || _disposed)
                return;

            _paused = true;
            Subscribe();

            int scenes = SceneManager.sceneCount;
            for (int i = 0; i < scenes; i++)
                FreezeScene(SceneManager.GetSceneAt(i));

            Scene persistent = DontDestroyOnLoadScene.Get();
            if (persistent.IsValid())
                FreezeScene(persistent);
        }

        public void OnResume()
        {
            if (!_paused)
                return;

            _paused = false;
            for (int i = 0; i < _frozen.Count; i++)
            {
                T component = _frozen[i];
                if (component != null)
                    Thaw(component);
            }

            _frozen.Clear();
            _frozenSet.Clear();
        }

        /// <summary>Freezes the active components of a scene that came while the game stands; nothing if it runs.</summary>
        public void FreezeScene(Scene scene)
        {
            if (!_paused || !scene.IsValid() || !scene.isLoaded)
                return;

            s_roots.Clear();
            scene.GetRootGameObjects(s_roots);
            for (int r = 0; r < s_roots.Count; r++)
            {
                GameObject root = s_roots[r];
                if (root == null || !root.activeInHierarchy)
                    continue;

                _found.Clear();
                root.GetComponentsInChildren(false, _found);
                for (int c = 0; c < _found.Count; c++)
                    TryFreeze(_found[c]);
            }

            s_roots.Clear();
            _found.Clear();
        }

        /// <summary>Drops the sceneLoaded subscription; a disposed freezer freezes nothing more.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            OnResume();
            if (_subscribed)
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            _subscribed = false;
            _disposed = true;
        }

        /// <summary>Stops the component; true — it has to be given back on resume.</summary>
        protected abstract bool Freeze(T component);

        /// <summary>Gives back what <see cref="Freeze"/> took.</summary>
        protected abstract void Thaw(T component);

        private void Subscribe()
        {
            if (_subscribed)
                return;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            _subscribed = true;
        }

        private void TryFreeze(T component)
        {
            if (component == null || _frozenSet.Contains(component) || !Freeze(component))
                return;

            _frozenSet.Add(component);
            _frozen.Add(component);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_paused)
                FreezeScene(scene);
        }
    }
}
