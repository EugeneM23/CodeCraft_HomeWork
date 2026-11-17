namespace Saving.Objects
{
    /// <summary>Hooks for components of a Saveable: before its state is captured, after its saved data is applied.</summary>
    public interface ISaveCallbacks
    {
        void OnBeforeSave();
        void OnAfterLoad();
    }
}
