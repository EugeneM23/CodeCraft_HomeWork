using Newtonsoft.Json.Linq;

namespace Saving
{
    /// <summary>Writes and reads one section of a save: one system's data under one key.</summary>
    public interface IGameSerializer
    {
        string Key { get; }                           // section key, unique among registered serializers
        int Version { get; }                          // current data version, starts at 1
        int Order { get; }                            // restore order, lower first
        JToken Capture();
        JToken Upgrade(JToken data, int fromVersion); // one step: fromVersion -> fromVersion + 1
        void Restore(JToken data);                    // data is already at Version
    }
}
