using UnityEngine;

namespace Modules.PlayerController
{
    internal class PushTrigger : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerController player))
            {
                player.OnCollisionEnter2D(null);
                Vector2 velocity = player.Velocity;
                player.AddImpulse(-transform.up * 40 + -(Vector3)velocity);
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