namespace Saving.UI
{
    /// <summary>What picking a slot in the menu does.</summary>
    public enum SaveSlotsMenuMode
    {
        Load,  // load the slot; empty slots cannot be picked
        Save   // save into the slot; a taken one asks first
    }
}
