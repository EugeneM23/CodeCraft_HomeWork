using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Like <see cref="CollisionEventReceiver"/>, but also reports contacts that last: a body
    /// pressing into something, not only striking it.
    ///
    /// A separate component on purpose. Unity calls OnCollisionStay on every physics step for as
    /// long as a contact lasts, for every component that declares it; on the shared receiver that
    /// would have meant a call per step for every bullet, casing and grenade lying on the floor.
    /// </summary>
    public sealed class CollisionContactReceiver : MonoBehaviour
    {
        public event Action<Collision> OnEntered;
        public event Action<Collision> OnStayed;

        private void OnCollisionEnter(Collision collision) => this.OnEntered?.Invoke(collision);

        private void OnCollisionStay(Collision collision) => this.OnStayed?.Invoke(collision);
    }
}
