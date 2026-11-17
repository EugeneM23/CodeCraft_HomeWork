using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Saving.Objects
{
    /// <summary>A Saveable in a scene section: its active flag, its children's and the data of its components by key.</summary>
    internal class ObjectData
    {
        public bool Active = true;
        public Dictionary<string, bool> Children = new();
        public Dictionary<string, ComponentData> Components = new();

        /// <summary>
        /// Plain objects the game keeps for this object outside its components (classes of a DI container), given by
        /// an ISaveStateProvider: "&lt;component key&gt;:&lt;class&gt;" to the object's members. Absent when there are none.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JToken> State;

        public void CopyFrom(ObjectData other)
        {
            Active = other.Active;
            Children = other.Children;
            Components = other.Components;
            State = other.State;
        }
    }

    /// <summary>One component: its data version, its enabled flag (for components that have one) and its data.</summary>
    internal sealed class ComponentData
    {
        public int V;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? Enabled;
        public JToken Data;
    }
}
