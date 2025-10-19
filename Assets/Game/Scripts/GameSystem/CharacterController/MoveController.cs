using Gamplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Scripts.Modules.CharacterController
{
    public class MoveController : MonoBehaviour, GameInput.IMovementActions
    {
        [SerializeField] private float _speed = 5f;
        [SerializeField] private BulletFall _bulletFall;

        private Vector2 _direction;
        private GameInput _gameInput;

        private void OnEnable()
        {
            _gameInput = new GameInput();
            _gameInput.Movement.Enable();
            _gameInput.Movement.AddCallbacks(this);
        }

        private void Update()
        {
            _bulletFall.SimulateFall(_speed);

            Vector3 velocity = new Vector3(_direction.x * _speed, _bulletFall.VelocityY, 0f);
            transform.position += velocity * Time.deltaTime;
        }

        public void OnWASD(InputAction.CallbackContext context)
        {
            _direction = context.ReadValue<Vector2>();
        }
    }
}