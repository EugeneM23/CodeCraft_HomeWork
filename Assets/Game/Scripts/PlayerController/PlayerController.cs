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

        public Vector2 Velocity => _frameVelocity;

        public Vector2 _frameVelocity;

        [ShowInInspector] private Vector2 _debugNormal;
        [ShowInInspector] private bool _debugIsGround;
        [ShowInInspector] private bool _debugIsceiling;
        [ShowInInspector] private Vector2 _debugFrameVelocity;
        
        private SlopeSlideComponent _slopeSlideComponent;
        private BounceComponent _bounceComponent;

        private void Start()
        {
            _collision = new CollisionComponent(_collider, _stats, this);
            _inputHandler = new InputHandler();
            _gravityComponent = new GravityComponent(_stats, _collision, this);
            _moveComponent = new MoveComponentDva(_stats, _collision, this);
            _jumpComponent = new JumpComponent(_stats, _inputHandler);
            _slopeSlideComponent = new SlopeSlideComponent(_collision, 89, 2);
            _bounceComponent = new BounceComponent(_collision, this, 5f, 10);
            OnJump += _gravityComponent.Reset;
        }

        private void Update()
        {
            _inputHandler.HandleInput();
        }

        private void FixedUpdate()
        {
            _collision.DetectCollisions();


            _frameVelocity = _moveComponent.Move(_frameInput.Move);
            _frameVelocity.y = _gravityComponent.GetYVelocity();
            _frameVelocity += _slopeSlideComponent.GetSlideVelocity();
            _frameVelocity.y += _jumpComponent.HandleJump();
            //_frameVelocity.y += _bounceComponent.GetBounceImpulse();

            _rigidbody.linearVelocity = _frameVelocity;

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

    public class BounceComponent
    {
        private readonly CollisionComponent _collision;
        private readonly PlayerController _player;
        private readonly float _flatSurfaceAngle;
        private readonly float _bounceMultiplier;

        private bool _wasGrounded;
        private bool _canBounce;
        private float _landingVelocity;

        public BounceComponent(CollisionComponent collision, PlayerController player, float flatSurfaceAngle = 5f,
            float bounceMultiplier = 0.5f)
        {
            _collision = collision;
            _player = player;
            _flatSurfaceAngle = flatSurfaceAngle;
            _bounceMultiplier = bounceMultiplier;
            _canBounce = true;
        }

        public float GetBounceImpulse()
        {
            bool isGroundedNow = _collision.IsGrounded;

            if (!_wasGrounded && isGroundedNow)
            {
                Debug.Log("Grounded ---------------------------");
                Debug.Log("Landed! Velocity: " + _player.Velocity.y);
                Vector2 normal = _collision.SurfaceNormal;
                float angle = Vector2.Angle(normal, Vector2.up);
                Debug.Log("Surface angle: " + angle + " CanBounce: " + _canBounce);


                _landingVelocity = _player.Velocity.y;
                _canBounce = false;
                _wasGrounded = true;

                float bounce = Mathf.Abs(_landingVelocity) * _bounceMultiplier;
                Debug.Log("BOUNCE! Impulse: " + bounce);
                return _bounceMultiplier;
            }

            if (!isGroundedNow && _wasGrounded)
            {
                _canBounce = true;
                Debug.Log("Left ground - can bounce again");
            }

            _wasGrounded = isGroundedNow;
            return 0f;
        }
    }

    public class SlopeSlideComponent
    {
        private readonly CollisionComponent _collision;
        private readonly float _maxAngle;
        private readonly float _slideForce;

        public SlopeSlideComponent(CollisionComponent collision, float maxAngle = 45f, float slideForce = 5f)
        {
            _collision = collision;
            _maxAngle = maxAngle;
            _slideForce = slideForce;
        }

        public Vector2 GetSlideVelocity()
        {
            if (!_collision.IsGrounded) return Vector2.zero;

            Vector2 normal = _collision.SurfaceNormal;
            if (normal == Vector2.zero) return Vector2.zero;

            float angle = Vector2.Angle(normal, Vector2.up);
            if (angle > _maxAngle) return Vector2.zero;

            Vector2 slideDir = Vector3.Cross(Vector3.forward, normal);
            slideDir = new Vector2(slideDir.x, slideDir.y).normalized;

            if (slideDir.y > 0) slideDir = -slideDir;

            float multiplier = angle / _maxAngle;

            return slideDir * (_slideForce * multiplier);
        }
    }
}