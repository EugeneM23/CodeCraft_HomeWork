using System;
using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using Gameplay;
using Sirenix.OdinInspector;
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

        public Vector2 SurfaceNormal => _collision.SurfaceNormal;
        public Vector2 Velocity => _rigidbody.linearVelocity;
        public bool IsOnStairs { get; set; }
        public bool IsOnSlope => Vector2.Angle(Vector2.right, _collision.SurfaceNormal) < 89;

        public Vector2 _frameVelocity;

        [ShowInInspector] private Vector2 _debugNormal;
        [ShowInInspector] private bool _debugIsGround;
        [ShowInInspector] private bool _debugIsceiling;
        [ShowInInspector] private Vector2 _debugFrameVelocity;

        private SlopeSlideComponent _slopeSlideComponent;
        private BounceComponent _bounceComponent;
        private StairsMoveComponent _stairsMove;

        private void Start()
        {
            _rigidbody.gravityScale = 0;
            _collision = new CollisionComponent(_collider, _stats, this);
            _inputHandler = new InputHandler();
            _gravityComponent = new GravityComponent(_stats, _collision, this);
            _moveComponent = new MoveComponent(_stats, _collision, this);
            _jumpComponent = new JumpComponent(_stats, _inputHandler, this);
            _slopeSlideComponent = new SlopeSlideComponent(_collision, 89, 3);
            _bounceComponent = new BounceComponent(_collision, this, 5f, 10);
            _stairsMove = new StairsMoveComponent(_stats, _collision, this);
        }

        private void Update()
        {
            _collision.SurfaceNormal.Log();
            IsOnSlope.Log();
            _inputHandler.HandleInput();
        }

        private void FixedUpdate()
        {
            _collision.DetectCollisions();
            Vector2 move = Vector2.zero;

            move += _stairsMove.Move(_frameInput.Move);
            move += _moveComponent.Move(_frameInput.Move);
            move += _gravityComponent.GetGravityVector();
            //move += _slopeSlideComponent.GetSlideVelocity();
            move += _jumpComponent.GetJumpVector();

            _rigidbody.linearVelocity = move;

            debug();
        }

        private void debug()
        {
            _debugIsGround = IsGrounded;
            _debugIsceiling = IsCeilingHit;
            _debugFrameVelocity = _frameVelocity;
            _debugNormal = _collision.SurfaceNormal;

            if (IsCeilingHit)
            {
                Debug.Log("Ceiling hit");
            }
        }
    }
}