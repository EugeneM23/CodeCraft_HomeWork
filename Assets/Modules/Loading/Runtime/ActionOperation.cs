using System;

namespace Loading
{
    /// <summary>An instant step: calls the action in Begin and succeeds. Weightless by default.</summary>
    public sealed class ActionOperation : LoadingOperation
    {
        private readonly Action _action;

        public ActionOperation(Action action, float weight = 0f) : base(weight)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        protected override void OnBegin()
        {
            _action();
            Succeed();
        }
    }
}
