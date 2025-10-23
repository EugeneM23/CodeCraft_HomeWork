using System;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private CapsuleCollider2D _collider;
        [SerializeField] private ScriptableStats _stats;

        private FrameInput _frameInput;
        private Vector2 _frameVelocity;
        private bool _grounded;
        private bool _jumpToConsume;
        private float _fallMultiplier;

        private void Start()
        {
            Application.targetFrameRate = 120;
            _fallMultiplier = _stats.FallMultiplier;
            Physics2D.queriesStartInColliders = false;
        }

        private void Update()
        {
            _frameInput = new FrameInput
            {
                Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                JumpDown = Input.GetKeyDown(KeyCode.Space),
                JumpHeld = Input.GetKey(KeyCode.Space)
            };

            if (_frameInput.JumpDown)
            {
                _jumpToConsume = true;
            }
        }

        private void FixedUpdate()
        {
            CheckCollisions();
            HandleDirection();
            HandleGravity();
            HandleJump();

            ApplyMovement();
        }

        private void HandleJump()
        {
            if (_jumpToConsume)
            {
                ExecuteJump();
                _jumpToConsume = false;
            }
        }

        private void ExecuteJump()
        {
            _frameVelocity.y = _stats.JumpPower;
        }

        private void HandleGravity()
        {
            if (_grounded && _frameVelocity.y <= 0f)
            {
                _fallMultiplier = _stats.FallMultiplier;
                _frameVelocity.y = _stats.GroundingForce;
            }
            else
            {
                float inAirGravity = _stats.FallAcceleration * (1 + _fallMultiplier);

                _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, -_stats.MaxFallSpeed,
                    inAirGravity * Time.fixedDeltaTime);

                _fallMultiplier += _stats.FallMultiplier;
            }
        }

        private void ApplyMovement()
        {
            _rigidbody.linearVelocity = _frameVelocity;
        }

        private void HandleDirection()
        {
            if (_frameInput.Move.x == 0)
            {
                var deceleration = _grounded ? _stats.GroundDeceleration : _stats.AirDeceleration;
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
            }
            else
            {
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, _frameInput.Move.x * _stats.MaxSpeed,
                    _stats.Acceleration * Time.fixedDeltaTime);
            }
        }

        private void CheckCollisions()
        {
            Vector2 origin = _collider.bounds.center;
            // Ground and Ceiling
            bool groundHit = Physics2D.CapsuleCast(_collider.bounds.center, _collider.size, _collider.direction, 0,
                Vector2.down, _stats.GrounderDistance, _stats.PlayerLayer);

            bool ceilingHit = Physics2D.CapsuleCast(_collider.bounds.center, _collider.size, _collider.direction, 0,
                Vector2.up, _stats.GrounderDistance, _stats.PlayerLayer);

            Drawdebug(origin, groundHit, ceilingHit);

            // Hit a Ceiling
            if (ceilingHit)
                _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

            if (!_grounded && groundHit)
            {
                _grounded = true;
            }

            else if (_grounded && !groundHit)
            {
                _grounded = false;
            }
        }

        private void Drawdebug(Vector2 origin, bool groundHit, bool ceilingHit)
        {
            // Правильная визуализация: от края коллайдера
            float halfHeight = _collider.size.y / 2f;
            Vector2 bottomPoint = origin + Vector2.down * halfHeight;
            Vector2 topPoint = origin + Vector2.up * halfHeight;

            Debug.DrawRay(bottomPoint, Vector2.down * _stats.GrounderDistance, groundHit ? Color.green : Color.red);
            Debug.DrawRay(topPoint, Vector2.up * _stats.GrounderDistance, ceilingHit ? Color.green : Color.red);
        }
    }

    public struct FrameInput
    {
        public Vector2 Move;
        public bool JumpDown;
        public bool JumpHeld;
    }
}