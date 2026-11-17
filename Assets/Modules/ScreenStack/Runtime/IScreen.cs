namespace ScreenNavigation
{
    /// <summary>
    /// A screen as ScreenStack sees it. Three pairs, one meaning each: Open/Close — the screen entered or left the
    /// stack, Show/Hide — it became visible or invisible, Focus/Blur — it got or lost input. A screen covered by an
    /// opaque one is hidden but stays in the stack, which is how it tells "covered" from "closed".
    /// Only ScreenStack calls these methods.
    /// </summary>
    public interface IScreen
    {
        /// <summary>Screens below an overlay stay visible (a dialog over settings).</summary>
        bool IsOverlay { get; }

        /// <summary>False: Back() keeps the screen in the stack (the main menu root).</summary>
        bool CanGoBack { get; }

        void Open();
        void Close();
        void Show();
        void Hide();
        void Focus();
        void Blur();
    }
}
