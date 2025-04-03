using System;
using UnityEngine;

namespace Modules.PrefabPool
{
    public interface IDespawned
    {
        public event Action<GameObject> DeSpawn;
        
        public void Destroy();
    }
}