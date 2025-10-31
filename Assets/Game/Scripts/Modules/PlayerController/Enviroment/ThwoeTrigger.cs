using System;
using UnityEngine;

namespace Modules.PlayerController
{
    public class ThrowItemTrigger : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private float _power = 50;

        private void OnTriggerStay2D(Collider2D other)
        {
            Debug.Log("Stay");
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (other.TryGetComponent(out PlayerController player))
                {
                    _rigidbody.bodyType = RigidbodyType2D.Dynamic;
                    Vector3 direction = (transform.position - player.transform.position).normalized;
                    direction.y = 0;
                    _rigidbody.AddForce(direction * _power, ForceMode2D.Impulse);
                }
            }
        }
    }
}