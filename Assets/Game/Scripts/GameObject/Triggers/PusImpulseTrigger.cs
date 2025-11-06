using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Triggers
{
    internal class PusImpulseTrigger : MonoBehaviour
    {
        [SerializeField] private int _impulsePower = 20;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out CharacterController2D player))
            {
                player.OnCollisionEnter2D(null);
                Vector2 velocity = player.Velocity;
                player.AddImpulse(-transform.up * _impulsePower + -(Vector3)velocity);
            }
        }
    }
}