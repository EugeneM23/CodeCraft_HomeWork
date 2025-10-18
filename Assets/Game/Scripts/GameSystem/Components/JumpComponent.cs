using System;

namespace Gameplay
{
    public class JumpComponent
    {
        public event Action OnJump;

        private int _maxJumps = 2;
        private int _currentJumps;

        private readonly CompositCondition condition = new();

        public void TryJump()
        {
            if (condition.IsTrue() || _currentJumps >= _maxJumps)
                return;

            _currentJumps++;
            OnJump?.Invoke();
        }

        public void RestJumps() => _currentJumps = 0;
    }
}