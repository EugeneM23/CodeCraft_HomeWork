namespace Ticking
{
    /// <summary>Every frame while the game runs — the game loop's Update. Not called while paused.</summary>
    public interface IUpdateListener
    {
        void OnUpdate(float deltaTime);
    }
}
