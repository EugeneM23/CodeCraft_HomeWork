using System;
using UnityEngine;

namespace Modules.PrefabPool
{
    public interface IDespawned
    {
        public event Action<IDespawned> BackToPool;
        void SetDespawnCallBack(System.Action<GameObject> callback);
    }
}