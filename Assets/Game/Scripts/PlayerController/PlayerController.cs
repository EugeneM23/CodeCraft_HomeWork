using System;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    using System;
    using Gameplay;
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

            public bool IsGrounded => _collisionComponent.IsGrounded;
            public bool IsCeilingHit => _collisionComponent.IsCeilingHit;
            private FrameInput _frameInput => _inputHandler.FrameInput;

            private Vector2 _frameVelocity;
            private float _fallMultiplier;

            private void Start()
            {
                Physics2D.queriesStartInColliders = false;
                _collisionComponent = new CollisionComponent(_collider, _stats);
                _inputHandler = new InputHandler();
                
            }

            private void Update()
            {
                _inputHandler.HandleInput();
            }

            private void FixedUpdate()
            {
                _collisionComponent.DetectCollisions();

                HandleDirection();
                HandleGravity();
                HandleJump();

                ApplyMovement();
            }

            private void HandleJump()
            {
                if (_inputHandler.JumpToConsume)
                {
                    ExecuteJump();
                    _inputHandler.ConsumeJump();
                }
            }

            private void ExecuteJump()
            {
                _frameVelocity.y = _stats.JumpPower;
            }

            private void HandleGravity()
            {
                if (IsGrounded && _frameVelocity.y <= 0f)
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
                if (IsCeilingHit)
                    _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

                _rigidbody.linearVelocity = _frameVelocity;
            }

            private void HandleDirection()
            {
                if (_frameInput.Move.x == 0)
                {
                    var deceleration = IsGrounded
                        ? _stats.GroundDeceleration
                        : _stats.AirDeceleration;
                    _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
                }
                else
                {
                    _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x,
                        _frameInput.Move.x * _stats.MaxSpeed,
                        _stats.Acceleration * Time.fixedDeltaTime);
                }
            }
        }
    }
}