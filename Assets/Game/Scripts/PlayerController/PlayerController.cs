using System;
using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private ScriptableStats _stats;
        [SerializeField] private CapsuleCollider2D _collider;

        private CollisionComponent _collision;
        private InputHandler _inputHandler;
        private GravityComponent _gravityComponent;
        private IMoveComponent _moveComponent;
        private JumpComponent _jumpComponent;

        public event Action OnJump;
        public event Action OnLand;
        public event Action OnGrounded;
        public event Action OnHitCeiling;

        public bool IsGrounded => _collision.IsGrounded;
        public bool IsCeilingHit => _collision.IsCeilingHit;
        private FrameInput _frameInput => _inputHandler.FrameInput;

        public Vector2 Velocity => _frameVelocity;

        private Vector2 _frameVelocity;

        private void Start()
        {
            _collision = new CollisionComponent(_collider, _stats);
            _inputHandler = new InputHandler();
            _gravityComponent = new GravityComponent(_stats, _collision, this);
            _moveComponent = new MoveComponentDva(_stats, _collision, this);
            _jumpComponent = new JumpComponent(_stats, _inputHandler);

            OnJump += _gravityComponent.Reset;
        }

        private void Update() => _inputHandler.HandleInput();

        private void FixedUpdate()
        {
            _collision.DetectCollisions();

            _frameVelocity = _moveComponent.Move(_frameInput.Move);

            float yVelocity = _gravityComponent.GetYVelocity();

            if (yVelocity != 0)
                _frameVelocity.y = yVelocity;

            _frameVelocity.y += _jumpComponent.HandleJump();

            _rigidbody.linearVelocity = _frameVelocity;
        }
    }
}