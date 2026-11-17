namespace Game
{
    /// <summary>
    /// A screen that wants a say when the player presses Esc / Back on it: cancel a key rebind,
    /// ask about unsaved settings. True = handled, the screen stays.
    /// </summary>
    public interface IBackHandler
    {
        bool HandleBack();
    }
}
