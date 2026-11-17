namespace Ticking
{
    /// <summary>Every frame while the game runs, after every OnUpdate — the game loop's LateUpdate.</summary>
    public interface ILateUpdateListener
    {
        void OnLateUpdate(float deltaTime);
    }
}
