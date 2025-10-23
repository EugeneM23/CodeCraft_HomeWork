using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private ScriptableStats _stats;
        [SerializeField] private CapsuleCollider2D _collider;

        private CollisionComponent _collisionComponent;
        private InputHandler _inputHandler;
        private GravityComponent _gravityComponent;
        private MoveComponent _moveComponent;
        private JumpComponent _jumpComponent;

        public bool IsGrounded => _collisionComponent.IsGrounded;
        public bool IsCeilingHit => _collisionComponent.IsCeilingHit;
        private FrameInput _frameInput => _inputHandler.FrameInput;

        private Vector2 _frameVelocity;

        private void Start()
        {
            Physics2D.queriesStartInColliders = false;
            
            _collisionComponent = new CollisionComponent(_collider, _stats);
            _inputHandler = new InputHandler();
            _gravityComponent = new GravityComponent(_stats);
            _moveComponent = new MoveComponent(_stats);
            _jumpComponent = new JumpComponent(_stats, _inputHandler);
        }

        private void Update() => _inputHandler.HandleInput();

        private void FixedUpdate()
        {
            _collisionComponent.DetectCollisions();

            _frameVelocity.x = _moveComponent.Move(_frameInput.Move.x, IsGrounded, _frameVelocity.x);
            _frameVelocity.y = _gravityComponent.GetGravity(IsGrounded, _frameVelocity.y, IsCeilingHit);
            _frameVelocity.y += _jumpComponent.HandleJump();

            _rigidbody.linearVelocity = _frameVelocity;
        }
    }
}