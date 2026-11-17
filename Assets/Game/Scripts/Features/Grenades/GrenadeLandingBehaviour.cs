using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Brakes the grenade from its first contact on, so it comes to rest near where it was aimed.
    ///
    /// The throw lands the grenade at 10 m/s or more along the ground. Rolling, a sphere loses next
    /// to nothing and went on 7-8 m past the cursor; held from rolling, it slid on friction about as
    /// far. In the air there is no damping, so the arc GrenadeTrajectory computed holds exactly; from
    /// the first bounce (floor, wall, anything) the body drags to a stop within a metre or so.
    /// </summary>
    public sealed class GrenadeLandingBehaviour : IEntityInit, IEntityEnable, IEntityDispose
    {
        private readonly Rigidbody _body;
        private readonly CollisionEventReceiver _receiver;
        private readonly float _groundDamping;

        public GrenadeLandingBehaviour(Rigidbody body, CollisionEventReceiver receiver, float groundDamping)
        {
            _body = body;
            _receiver = receiver;
            _groundDamping = groundDamping;
        }

        public void Init(IEntity entity) => _receiver.OnEntered += OnCollision;

        public void Dispose(IEntity entity) => _receiver.OnEntered -= OnCollision;

        // Rented again from the pool: in flight once more.
        public void Enable(IEntity entity) => _body.linearDamping = 0f;

        private void OnCollision(Collision collision) => _body.linearDamping = _groundDamping;
    }
}
