using UnityEngine;

namespace Game.Scripts.Modules.PlayerController.Enviroment
{
    internal class StairsTrigger : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerController player))
            {
                player.ResetVelocity();
                player.IsOnStairs = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerController player))
            {
                player.OnCollisionEnter2D(null);

                player.IsOnStairs = false;
            }
        }
    }
}