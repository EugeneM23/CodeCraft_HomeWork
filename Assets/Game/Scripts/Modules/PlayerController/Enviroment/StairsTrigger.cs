using UnityEngine;

namespace Modules.PlayerController
{
    internal class StairsTrigger : MonoBehaviour
    {
        private bool _isOn;

        private void OnTriggerStay2D(Collider2D other)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) && !_isOn)
                Enter(other);

            if (_isOn && (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.Space)))
                Exit(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_isOn)
            {
                Exit(other);
                _isOn = false;
            }
        }

        private void Enter(Collider2D other)
        {
            _isOn = true;
            if (other.TryGetComponent(out CharacterController2D player))
            {
                player.ResetVelocity();
                //player.SetComponent<StairsMoveComponent>();
                player.IsOnStairs = true;
                player.transform.position =
                    new Vector3(transform.position.x, player.transform.position.y, player.transform.position.z);
            }
        }

        private void Exit(Collider2D other)
        {
            if (other.TryGetComponent(out CharacterController2D player))
            {
                player.OnCollisionEnter2D(null);
                //player.SetComponent<MoveComponent>();
                player.IsOnStairs = false;
            }
        }
    }
}