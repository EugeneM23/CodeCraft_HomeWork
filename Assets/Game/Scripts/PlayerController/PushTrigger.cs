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
                player.AddImpulse(-transform.up * 50);
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