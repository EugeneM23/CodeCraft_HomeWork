using Modules.PlayerController;
using Unity.VisualScripting;
using UnityEngine;

namespace Gameplay
{
    public class BreakableSurfaceTrigger : MonoBehaviour
    {
        [SerializeField] private Transform _prefab;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out Entity entity))
            {
                Character character = entity.GetEntityComponent<Character>();

                if (character is Player)
                {
                    var controller = entity.GetEntityComponent<CharacterController2D>();

                    if (controller.LastFrameVelocity.magnitude > 50)
                    {
                        Transform instantiate = Instantiate(_prefab, transform.position, Quaternion.identity);
                        Rigidbody2D[] components = instantiate.GetComponentsInChildren<Rigidbody2D>();

                        foreach (var rb in components)
                        {
                            Vector2 randomDir = Random.insideUnitCircle.normalized;

                            rb.AddForce(randomDir * 20, ForceMode2D.Impulse);
                            rb.AddTorque(50f);
                        }

                        Destroy(gameObject);
                    }
                }
            }
        }
    }
}