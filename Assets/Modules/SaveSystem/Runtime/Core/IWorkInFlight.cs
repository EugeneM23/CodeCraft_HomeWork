namespace Saving
{
    /// <summary>
    /// Work the game has started and not finished yet, whose end changes the state of the game: a coin flying to the
    /// bank that pays when it lands, a door that unlocks when its animation ends. Such a callback cannot go into a
    /// save, so the save system does not save in the middle of it: <see cref="SaveLoadSystem"/> waits before saving by
    /// itself (autosave, keys, buttons), finishes the work at once before saving on quit and app pause, and drops it
    /// before a load. The default one knows DOTween (found by reflection, no reference); set
    /// <see cref="SaveLoadSystem.WorkInFlight"/> for another tween library, or null to switch it off.
    /// </summary>
    public interface IWorkInFlight
    {
        /// <summary>Something is running that will change the game when it ends.</summary>
        bool IsBusy { get; }

        /// <summary>Ends it now, the way it would end by itself (its callbacks run): the app is about to stop.</summary>
        void Finish();

        /// <summary>Stops it without its end (no callbacks): a load replaces the game it was started in.</summary>
        void Drop();
    }
}
