using System;
using UnityEngine;

namespace Modules.Pooling
{
    public interface IDespawned
    {
        public event Action<GameObject> DeSpawn;
        
        public void Destroy();
    }
}