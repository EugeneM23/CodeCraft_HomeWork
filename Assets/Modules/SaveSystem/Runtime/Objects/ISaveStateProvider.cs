using System.Collections.Generic;

namespace Saving.Objects
{
    /// <summary>
    /// On a component of a saved object: hands over the plain objects the game keeps for it outside its components —
    /// classes of a DI container, a model behind a view (health, a clip, a cooldown). Each is saved with the object
    /// under its class name and gets its data back on a load, into the same instance: its [Save] members, or without
    /// any its public values and the simple values in its private fields and auto-properties
    /// (<see cref="SaveMembers.State"/>). Two objects of one class are told apart by their order, so keep the order
    /// the same. An object that implements <see cref="ISaveCallbacks"/> is told before a save and after a load —
    /// the place to raise the events its views listen to. Unity objects in the list are skipped: components are
    /// saved as components.
    /// </summary>
    public interface ISaveStateProvider
    {
        void CollectState(List<object> state);
    }
}
