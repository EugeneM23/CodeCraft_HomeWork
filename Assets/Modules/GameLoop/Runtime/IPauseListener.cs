namespace Ticking
{
    /// <summary>
    /// The game stopped or went on. For whatever does not live on the loop's ticks and still has to stand still: input
    /// to switch off, and freezers of things the engine moves by itself (physics, animation, particles, sound). A
    /// listener added while the game is paused gets OnPause at once.
    /// </summary>
    public interface IPauseListener
    {
        void OnPause();

        void OnResume();
    }
}
