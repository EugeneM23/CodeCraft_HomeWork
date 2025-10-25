using System;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class StairsTrigger : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerController player)) 
                player.IsOnStairs = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerController player)) 
                player.IsOnStairs = false;
        }
    }
}