using System;
using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using Gameplay;
using Sirenix.OdinInspector;
using UnityEditor;
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
        private SlopeSlideComponent _slopeSlideComponent;
        private StairsMoveComponent _stairsMove;
        private WallSlidingComponent _wallSliding;
        private LedgeGrabComponent _ledgeGrabComponent;
        private SpeedComponent _inert;
        private MovingPlatformComponent _movePlatform;
        public event Action OnJump;
        public event Action OnLand;
        public event Action OnGrounded;
        public event Action OnHitCeiling;

        public bool IsGrounded => _collision.IsGrounded;
        public bool IsCeilingHit => _collision.IsCeilingHit;
        public FrameInput FrameInput => _inputHandler.FrameInput;

        public Vector2 SurfaceNormal => _collision.SurfaceNormal;
        public Vector2 Velocity => _rigidbody.linearVelocity;
        public bool IsOnStairs { get; set; }
        public bool IsOnSlope => Vector2.Angle(Vector2.right, _collision.SurfaceNormal) > 89;
        public bool IsGrabbingLedge => _ledgeGrabComponent.IsGrabbing;

        public Vector2 _frameVelocity;

        [ShowInInspector] private Vector2 _debugNormal;
        [ShowInInspector] private bool _debugIsGround;
        [ShowInInspector] private bool _debugIsceiling;
        [ShowInInspector] private Vector2 _debugFrameVelocity;
        [ShowInInspector] private bool _debugGrab;
        [ShowInInspector] private Vector2 _debugVelocity;

        private void Start()
        {
            Application.targetFrameRate = 120;
            Time.timeScale = 1f;
            _rigidbody.gravityScale = 0;
            _collision = new CollisionComponent(_collider, _stats, this);
            _inputHandler = new InputHandler();
            _gravityComponent = new GravityComponent(_stats, _collision, this);
            _inert = new SpeedComponent(_stats, this);
            _moveComponent = new MoveComponent(_collision, _inert, this);
            _wallSliding = new WallSlidingComponent(_collider, this, _stats, _collision);
            _ledgeGrabComponent = new LedgeGrabComponent(_collider, this, _stats.PlayerLayer);
            _jumpComponent = new JumpComponent(_stats, _inputHandler, this, _wallSliding, _ledgeGrabComponent);
            _slopeSlideComponent = new SlopeSlideComponent(_collision, this, 89f, 30f);
            _stairsMove = new StairsMoveComponent(_stats, _collision, this);
            _movePlatform = new MovingPlatformComponent(this, _collision, _stats);
        }

        private void Update()
        {
            _inputHandler.HandleInput();
        }

        private void FixedUpdate()
        {
            //_ledgeGrabComponent.CheckLedges();
            _collision.DetectCollisions();
            Vector2 move = Vector2.zero;

            move += _jumpComponent.GetJumpVector();
            move += _stairsMove.Move(FrameInput.Move);
           // move += _slopeSlideComponent.GetSlideVelocity();
            move += _moveComponent.Move(FrameInput.Move).Log();
            move += _gravityComponent.GetGravityVector();
            move += _wallSliding.ScanWall();
            move += _movePlatform.GetPlatformDelta();

            _rigidbody.linearVelocity = move;

            debug();
        }

        private void debug()
        {
            _debugIsGround = IsGrounded;
            _debugIsceiling = IsCeilingHit;
            _debugFrameVelocity = _frameVelocity;
            _debugNormal = _collision.SurfaceNormal;
            _debugGrab = IsGrabbingLedge;

            _debugVelocity = Velocity;
            if (IsCeilingHit)
            {
                Debug.Log("Ceiling hit");
            }
        }
    }
}