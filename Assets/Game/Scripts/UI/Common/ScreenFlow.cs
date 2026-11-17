using System;
using ScreenNavigation;

namespace Game
{
    /// <summary>
    /// The screen stack of one orchestrator (MenuFlow, BattleFlow) with what both need on top of it:
    /// Back that asks the top screen first (IBackHandler), and a Yes/No question whose answer arrives
    /// after the dialog has left the stack, so the answer may push or pop screens itself.
    /// </summary>
    public sealed class ScreenFlow : IDisposable
    {
        private readonly ConfirmDialog _dialog;
        private Action<bool> _answer;

        public ScreenStack Stack { get; } = new();

        public ScreenFlow(ConfirmDialog dialog)
        {
            _dialog = dialog;
            _dialog.Chosen += Answer;
        }

        public void Dispose() => _dialog.Chosen -= Answer;

        public void Push(IScreen screen)
        {
            Stack.Push(screen);
            AppServices.UiSounds.Play(UiSound.Open);
        }

        /// <summary>Shows the question over the current screen; Esc counts as No.</summary>
        public void Ask(string title, string text, string yes, string no, Action<bool> answer)
        {
            _dialog.Setup(title, text, yes, no);
            _answer = answer;
            Push(_dialog);
        }

        /// <summary>Esc / the Back button: the top screen may keep itself (IBackHandler), else it is popped if it can go back.</summary>
        public bool Back()
        {
            if (Stack.IsEmpty)
                return false;

            if (ReferenceEquals(Stack.Top, _dialog))
            {
                Answer(false);
                return true;
            }

            if (Stack.Top is IBackHandler handler && handler.HandleBack())
                return true;

            bool popped = Stack.Back();
            if (popped)
                AppServices.UiSounds.Play(UiSound.Back);
            return popped;
        }

        private void Answer(bool yes)
        {
            if (!ReferenceEquals(Stack.Top, _dialog))
                return;

            Stack.Pop();
            Action<bool> answer = _answer;
            _answer = null;
            answer?.Invoke(yes);
        }
    }
}
