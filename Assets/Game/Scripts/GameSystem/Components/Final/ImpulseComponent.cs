using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.Player
{
    public class ImpulseComponent : CompositCondition
    {
        private Rigidbody2D rigidbody;
        private float force;

        public ImpulseComponent(Rigidbody2D rigidbody, float force)
        {
            this.rigidbody = rigidbody;
            this.force = force;
        }

        public void AddForce()
        {
            if (AndCondition())
                return;

            this.rigidbody.linearVelocity = Vector2.zero;
            this.rigidbody.AddForce(Vector2.up * force, ForceMode2D.Impulse);
        }
    }
}