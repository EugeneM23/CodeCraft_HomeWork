using System;
using UnityEngine;

namespace Modules.Gameplay
{
    public sealed class CollisionEventReceiver : MonoBehaviour
    {
        public event Action<Collision> OnEntered;
        public event Action<Collision> OnExited;

        private void OnCollisionEnter(Collision collision)
        {
            this.OnEntered?.Invoke(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            this.OnExited?.Invoke(collision);
        }
    }
}