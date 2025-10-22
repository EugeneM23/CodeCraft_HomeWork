using Game.Scripts.GameObject.Player;
using UnityEngine;

namespace Gameplay
{
    public class Springboard : MonoBehaviour
    {
        [SerializeField] private float _impulsionForce = 15f;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerController controller))
            {
                controller.AddImpulse(_impulsionForce, transform.up);
            }
        }
    }
}