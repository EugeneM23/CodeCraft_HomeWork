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
        private StairsMoveComponent _stairsMove;
        private WallSlidingComponent _wallSliding;
        private LedgeGrabComponent _ledgeGrabComponent;
        private SpeedComponent _speed;
        private MovingPlatformComponent _movePlatform;

        public bool IsGrounded => _collision.IsGrounded;
        public bool IsCeilingHit => _collision.IsCeilingHit;
        public FrameInput FrameInput => _inputHandler.FrameInput;

        public Vector2 SurfaceNormal => _collision.SurfaceNormal;
        public Vector2 Velocity => _rigidbody.linearVelocity;
        public bool IsOnStairs { get; set; }
        public bool IsOnSlope => Vector2.Angle(Vector2.right, _collision.SurfaceNormal) > 89;
        public bool IsGrabbingLedge => _ledgeGrabComponent.IsGrabbing;

        public Vector2 _frameVelocity;
        private SlopeSlideComponent _slopeSlide;

        private void Start()
        {
            _rigidbody.gravityScale = 0;

            _collision = new CollisionComponent(_collider, _stats, this);
            _inputHandler = new InputHandler();
            _gravityComponent = new GravityComponent(_stats, _collision, this);
            _speed = new SpeedComponent(_stats);
            _moveComponent = new MoveComponent(_collision, _speed, this);
            _wallSliding = new WallSlidingComponent(_collider, this, _stats, _collision);
            _ledgeGrabComponent = new LedgeGrabComponent(_collider, this, _stats.PlayerLayer);
            _jumpComponent = new JumpComponent(_stats, _inputHandler, this, _wallSliding, _ledgeGrabComponent);
            _stairsMove = new StairsMoveComponent(_stats, _collision, this);
            _movePlatform = new MovingPlatformComponent(this, _collision, _stats);
            _slopeSlide = new SlopeSlideComponent(_collision, this);
        }

        private void Update()
        {
            _inputHandler.HandleInput();
        }

        private void FixedUpdate()
        {
            _collision.DetectCollisions();
            Vector2 move = Vector2.zero;

            move += _jumpComponent.GetJumpVector();
            move += _stairsMove.Move(FrameInput.Move);
            move += _moveComponent.Move(FrameInput.Move);
            move += _gravityComponent.GetGravityVector();
            move += _wallSliding.ScanWall();
            move += _movePlatform.GetPlatformDelta();
            move += _slopeSlide.GetSlideVelocity();

            _rigidbody.linearVelocity = move;
        }
    }
}