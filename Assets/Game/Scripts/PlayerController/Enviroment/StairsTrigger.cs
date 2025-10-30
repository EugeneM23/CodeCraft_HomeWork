using UnityEngine;

namespace PlayerController
{
    public class StairsTrigger : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerController player))
            {
                player.Restvelocity();
                player.IsOnStairs = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerController player))
            {
                player.Hit();
                
                player.IsOnStairs = false;
            }
        }
    }
}