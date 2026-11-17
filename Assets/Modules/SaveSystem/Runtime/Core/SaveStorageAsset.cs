using UnityEngine;

namespace Saving
{
    /// <summary>
    /// A storage picked in the Inspector of SaveLoadSystem ("Custom"): subclass it, create the asset and return your
    /// storage — a database, a platform save API, KeyValueSaveStorage over anything.
    /// </summary>
    public abstract class SaveStorageAsset : ScriptableObject
    {
        public abstract ISaveStorage CreateStorage();
    }
}
