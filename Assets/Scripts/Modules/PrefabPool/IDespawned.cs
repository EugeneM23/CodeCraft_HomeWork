using UnityEngine;

namespace Modules.PrefabPool
{
    public interface IDespawned
    {
        void SetDespawnCallBack(System.Action<GameObject> callback);
    }
}