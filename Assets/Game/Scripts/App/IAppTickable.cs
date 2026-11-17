namespace Game
{
    /// <summary>An app service that runs every frame on real time and never pauses (loading, autosave).</summary>
    public interface IAppTickable
    {
        void Tick(float unscaledDeltaTime);
    }
}
