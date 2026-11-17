using UnityEngine;

namespace Saving
{
    /// <summary>
    /// A server picked in the Inspector of SaveLoadSystem: subclass it, create the asset and return your
    /// IRemoteSaveStorage. SaveLoadSystem then keeps a SaveSync with it.
    /// </summary>
    public abstract class RemoteSaveStorageAsset : ScriptableObject
    {
        public abstract IRemoteSaveStorage CreateRemote();
    }
}
