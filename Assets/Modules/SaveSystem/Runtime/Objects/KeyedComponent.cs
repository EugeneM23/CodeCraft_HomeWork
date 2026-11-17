using UnityEngine;

namespace Saving.Objects
{
    /// <summary>A component of a Saveable with its key and the path of its object from the Saveable ("" for its own).</summary>
    internal readonly struct KeyedComponent
    {
        public KeyedComponent(string key, string path, Component component)
        {
            Key = key;
            Path = path;
            Component = component;
        }

        public string Key { get; }
        public string Path { get; }
        public Component Component { get; }
    }
}
