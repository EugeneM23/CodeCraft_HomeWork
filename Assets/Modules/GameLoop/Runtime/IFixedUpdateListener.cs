namespace Ticking
{
    /// <summary>Every fixed step while the game runs, before the physics step — the game loop's FixedUpdate.</summary>
    public interface IFixedUpdateListener
    {
        void OnFixedUpdate(float deltaTime);
    }
}
