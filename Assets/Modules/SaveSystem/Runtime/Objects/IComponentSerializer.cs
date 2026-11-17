using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>Writes and reads the state of one component type.</summary>
    public interface IComponentSerializer
    {
        Type ComponentType { get; }
        int Version { get; }                          // current data version, starts at 1
        JToken Capture(Component component);
        JToken Upgrade(JToken data, int fromVersion); // one step: fromVersion -> fromVersion + 1
        void Restore(Component component, JToken data); // data is already at Version
    }
}
