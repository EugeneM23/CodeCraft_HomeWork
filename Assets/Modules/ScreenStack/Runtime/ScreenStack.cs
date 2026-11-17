using System;
using System.Collections.Generic;

namespace ScreenNavigation
{
    /// <summary>
    /// Stack of menu screens. Only the top screen gets input; a screen is visible while it is the top one or every
    /// screen above it is an overlay. Inside one operation the calls go in a fixed order: Blur of the old top →
    /// Open of the entering screen → Hide (top-down) → Close of the leaving screens (top-down) → Show (bottom-up) →
    /// Focus of the new top → Changed. Plain C#: the owner keeps it as a field.
    /// </summary>
    public sealed class ScreenStack
    {
        /// <summary>After every operation that changed the stack; stack operations are allowed in handlers.</summary>
        public event Action Changed;

        private readonly List<IScreen> _screens = new();
        private readonly List<IScreen> _visible = new();
        private readonly List<IScreen> _nextVisible = new();
        private readonly List<IScreen> _leaving = new();
        private bool _busy;

        public IScreen Top => _screens.Count > 0 ? _screens[_screens.Count - 1] : null;
        public int Count => _screens.Count;
        public bool IsEmpty => _screens.Count == 0;

        public bool Contains(IScreen screen) => _screens.Contains(screen);

        public void Push(IScreen screen)
        {
            EnsureIdle();
            EnsureCanEnter(screen);

            Begin();
            try
            {
                Top?.Blur();
                _screens.Add(screen);
                screen.Open();
                ApplyVisibility();
                screen.Focus();
            }
            finally
            {
                End();
            }

            Changed?.Invoke();
        }

        public bool Pop()
        {
            EnsureIdle();
            if (IsEmpty)
                return false;

            Begin();
            try
            {
                IScreen leaving = Top;
                leaving.Blur();
                _screens.RemoveAt(_screens.Count - 1);
                _leaving.Add(leaving);
                ApplyVisibility();
                Top?.Focus();
            }
            finally
            {
                End();
            }

            Changed?.Invoke();
            return true;
        }

        public bool Back()
        {
            EnsureIdle();
            if (IsEmpty || !Top.CanGoBack)
                return false;

            return Pop();
        }

        public void Replace(IScreen screen)
        {
            EnsureIdle();
            if (IsEmpty)
            {
                Push(screen);
                return;
            }

            EnsureCanEnter(screen);

            Begin();
            try
            {
                IScreen leaving = Top;
                leaving.Blur();
                _screens[_screens.Count - 1] = screen;
                _leaving.Add(leaving);
                screen.Open();
                ApplyVisibility();
                screen.Focus();
            }
            finally
            {
                End();
            }

            Changed?.Invoke();
        }

        public void Clear()
        {
            EnsureIdle();
            if (IsEmpty)
                return;

            Begin();
            try
            {
                Top.Blur();
                for (int i = _screens.Count - 1; i >= 0; i--)
                    _leaving.Add(_screens[i]);
                _screens.Clear();
                ApplyVisibility();
            }
            finally
            {
                End();
            }

            Changed?.Invoke();
        }

        // Hide what is no longer visible (top-down), close what left (top-down), show what became visible (bottom-up)
        private void ApplyVisibility()
        {
            CollectVisible(_nextVisible);

            for (int i = _visible.Count - 1; i >= 0; i--)
            {
                if (!_nextVisible.Contains(_visible[i]))
                    _visible[i].Hide();
            }

            for (int i = 0; i < _leaving.Count; i++)
                _leaving[i].Close();
            _leaving.Clear();

            for (int i = 0; i < _nextVisible.Count; i++)
            {
                if (!_visible.Contains(_nextVisible[i]))
                    _nextVisible[i].Show();
            }

            _visible.Clear();
            _visible.AddRange(_nextVisible);
        }

        // From the top down to the first screen that is not an overlay; the result goes bottom-up
        private void CollectVisible(List<IScreen> result)
        {
            result.Clear();
            for (int i = _screens.Count - 1; i >= 0; i--)
            {
                result.Insert(0, _screens[i]);
                if (!_screens[i].IsOverlay)
                    break;
            }
        }

        private void Begin() => _busy = true;

        private void End()
        {
            _busy = false;
            _leaving.Clear();
        }

        private void EnsureIdle()
        {
            if (_busy)
                throw new InvalidOperationException("ScreenStack operations can't be called from screen callbacks.");
        }

        private void EnsureCanEnter(IScreen screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            if (_screens.Contains(screen))
                throw new InvalidOperationException($"Screen {screen} is already in the stack.");
        }
    }
}
