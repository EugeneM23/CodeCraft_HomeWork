using System;

namespace Loading
{
    /// <summary>
    /// Waits until the condition holds: checked in Begin and on every Tick, so a condition already true passes at
    /// once. A throwing condition fails the operation. Weightless by default: waiting is not loading progress.
    /// </summary>
    public sealed class WaitUntilOperation : LoadingOperation
    {
        private readonly Func<bool> _condition;

        public WaitUntilOperation(Func<bool> condition, float weight = 0f) : base(weight)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        protected override void OnBegin() => SucceedIfMet();

        protected override void OnTick(float dt) => SucceedIfMet();

        private void SucceedIfMet()
        {
            if (_condition())
                Succeed();
        }
    }
}
