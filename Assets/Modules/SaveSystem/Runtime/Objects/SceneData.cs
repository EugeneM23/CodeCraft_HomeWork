using System.Collections.Generic;

namespace Saving.Objects
{
    /// <summary>A scene section: its Saveables by id, the ids destroyed since the scene loaded, objects made in play.</summary>
    internal sealed class SceneData
    {
        public Dictionary<string, ObjectData> Objects = new();
        public List<string> Destroyed = new();
        public List<SpawnedData> Spawned = new();
    }

    /// <summary>An object made in play: restored from its prefab under its parent, with its nested Saveables.</summary>
    internal sealed class SpawnedData : ObjectData
    {
        public string Id;
        public string Prefab;
        public string Parent;      // id of the nearest Saveable above, if any
        public string ParentPath;  // from that Saveable, or from the scene root when there is none
        public Dictionary<string, NestedData> Nested = new();
    }

    /// <summary>A Saveable inside a spawned object, found by its path from the spawned root.</summary>
    internal sealed class NestedData : ObjectData
    {
        public string Id;
    }

    /// <summary>
    /// "$scenes": the scene that was active when the game saved and the scenes loaded next to it (additive ones: a
    /// scene with the game's systems, a HUD), in their order; left out when the active scene was the only one.
    /// </summary>
    internal sealed class ScenesData
    {
        public SceneRef Active;

        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public List<SceneRef> Additive;
    }

    internal sealed class SceneRef
    {
        public string Path;
        public string Guid;
    }
}
