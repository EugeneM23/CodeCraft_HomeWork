namespace Game
{
    /// <summary>Freezes and resumes the battle. Stub: Time.timeScale; later the core team's GameLoop.</summary>
    public interface IPauseService
    {
        bool IsPaused { get; }
        void Pause();
        void Resume();
    }
}
