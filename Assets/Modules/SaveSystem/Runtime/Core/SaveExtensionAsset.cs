using UnityEngine;

namespace Saving
{
    /// <summary>A settings asset that plugs a layer into SaveLoadSystem: SaveSystemSettings of the objects layer.</summary>
    public abstract class SaveExtensionAsset : ScriptableObject
    {
        public abstract ISaveExtension CreateExtension(SaveLoadService service);
    }
}
