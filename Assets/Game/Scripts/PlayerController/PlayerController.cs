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
        [SerializeField] private SurfaceTangentDebugger _debugger;

        private CollisionComponent _collisionComponent;
        private InputHandler _inputHandler;
        private GravityComponent _gravityComponent;
        private IMoveComponent _moveComponent;
        private JumpComponent _jumpComponent;

        public event Action OnJump;
        public event Action OnLand;
        public event Action OnGrounded;
        public event Action OnHitCeiling;

        public bool IsGrounded => _collisionComponent.IsGrounded;
        public bool IsCeilingHit => _collisionComponent.IsCeilingHit;
        private FrameInput _frameInput => _inputHandler.FrameInput;

        public Vector2 Velocity => _frameVelocity;
        private Vector2 _frameVelocity;

        private void Start()
        {
            Physics2D.queriesStartInColliders = false;

            _collisionComponent = new CollisionComponent(_collider, _stats);
            _inputHandler = new InputHandler();
            _gravityComponent = new GravityComponent(_stats);
            //_moveComponent = new MoveComponent(_stats, this);
            _moveComponent = new MoveComponentDva(_stats, this);
            _jumpComponent = new JumpComponent(_stats, _inputHandler);

            OnJump += _gravityComponent.Reset;
        }

        private void Update() => _inputHandler.HandleInput();

        private void FixedUpdate()
        {
            _collisionComponent.DetectCollisions();

            _frameVelocity = _moveComponent.Move(_frameInput.Move);
            _frameVelocity.y = _gravityComponent.GetGravity(IsGrounded, _frameVelocity.y, IsCeilingHit);
            _frameVelocity.y += _jumpComponent.HandleJump();

            _rigidbody.linearVelocity = _frameVelocity;
        }
    }
}