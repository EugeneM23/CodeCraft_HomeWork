using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.Player
{
    public class ImpulseComponent
    {
        private Rigidbody2D rigidbody;

        public ImpulseComponent(Rigidbody2D rigidbody)
        {
            this.rigidbody = rigidbody;
        }

        public void AddForce(Vector2 direction, float force)
        {
            this.rigidbody.linearVelocity = Vector2.zero;
            this.rigidbody.AddForce(direction * force, ForceMode2D.Impulse);
        }
    }
}