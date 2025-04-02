using UnityEngine;

namespace NewPool
{
    public interface IDespawned
    {
        void SetDespawnCallBack(System.Action<GameObject> callback);
    }
}