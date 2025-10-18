using Game.Scripts.GameObject.Player;
using UnityEngine;

namespace Gameplay
{
    public class Springboard : MonoBehaviour
    {
        [SerializeField] private float _impulsionForce = 70f;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out Entity entity))
            {
                if (entity.TryGetEntityComponent(out ImpulseComponent component))
                    component.AddForce(gameObject.transform.up, _impulsionForce);
            }
        }
    }
}