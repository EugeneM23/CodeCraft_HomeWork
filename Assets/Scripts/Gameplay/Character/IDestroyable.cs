using System;
using UnityEngine;

namespace Gameplay
{
    public interface IDestroyable
    {
        public event Action<GameObject> OnDestroy;
    }
}