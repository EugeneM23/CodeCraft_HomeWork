using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class PushTrigger : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerController player))
            {
                player.Hit();
                Vector2 velocity = player.Velocity;
                player.AddImpulse(-transform.up * 5 + -(Vector3)velocity);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerController player))
            {
            }
        }
    }
}