using System;

namespace Loading
{
    /// <summary>
    /// Runs one loading tree at a time: Run begins the root, the owner calls Tick every frame with its own dt (usually
    /// unscaled — the game may be paused). done is called exactly once, after IsRunning is reset, so the next loading
    /// may be started right from it.
    /// </summary>
    public sealed class LoadingRunner
    {
        private LoadingOperation _root;
        private Action<LoadingResult> _done;

        public bool IsRunning { get; private set; }

        /// <summary>Progress of the current (or last) root; 0 before the first run.</summary>
        public float Progress => _root?.Progress ?? 0f;

        /// <summary>Seconds since Run, as the sum of ticked dt.</summary>
        public float Elapsed { get; private set; }

        /// <summary>False — a loading is already running, the request is ignored. An instant tree finishes inside.</summary>
        public bool Run(LoadingOperation root, Action<LoadingResult> done = null)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (IsRunning)
                return false;

            _root = root;
            _done = done;
            Elapsed = 0f;
            IsRunning = true;

            root.Begin();
            FinishIfDone();
            return true;
        }

        public void Tick(float dt)
        {
            if (!IsRunning)
                return;

            Elapsed += dt;
            _root.Tick(dt);
            FinishIfDone();
        }

        private void FinishIfDone()
        {
            if (!_root.IsDone)
                return;

            IsRunning = false;
            Action<LoadingResult> done = _done;
            _done = null;

            LoadingResult result = _root.Status == LoadingStatus.Succeeded
                ? LoadingResult.Success
                : LoadingResult.Failure(_root.Error);
            done?.Invoke(result);
        }
    }
}
