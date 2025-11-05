using System;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Triggers
{
    public class ThrowItemTrigger : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private float _power = 50;

        private void OnTriggerStay2D(Collider2D other)
        {
            Debug.Log(other.name);
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (other.TryGetComponent(out CharacterController2D player))
                {
                    Debug.Log("Throw Item");
                    _rigidbody.bodyType = RigidbodyType2D.Dynamic;

                    Vector2 direction = new Vector2(player.LookDirection, 0);
                    _rigidbody.AddForce(direction * _power, ForceMode2D.Impulse);
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            Debug.Log(other.gameObject.name);
        }
    }
}