using System;

namespace Gameplay
{
    public class JumpComponent
    {
        public event Action OnJump;

        private int maxJumps = 2;
        private int currentJumps;

        private readonly CompositCondition condition = new();

        public void TryJump()
        {
            if (condition.IsTrue() || currentJumps >= maxJumps)
                return;

            this.currentJumps++;
            this.OnJump?.Invoke();
        }

        public void RestJumps() => currentJumps = 0;
    }
}