using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameSystem.CharacterController.Test
{
    public class GravityMoveComponent : MonoBehaviour
    {
        [SerializeField] private CapsuleCollider2D _capsule;
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float _gravity = 10;

        public bool IsGrounded { get; set; }

        private float _velocity;
        public Vector2 CurrentSurfaceNormal;
        private Vector2 _inputDir;

        private void Start() => Application.targetFrameRate = 100;

        private void Update()
        {
            Vector3 gravityMove = CalculateGravity();

            gravityMove = CheckCollision(gravityMove);

            Vector3 surfaceMove = CalculateSurfaceMove();

            transform.position += gravityMove + surfaceMove;

            Debug.DrawRay(transform.position, CurrentSurfaceNormal, Color.blue);
        }

        private Vector3 CalculateGravity()
        {
            _velocity += _gravity * Time.deltaTime;
            return Vector3.down * (_velocity * Time.deltaTime);
        }

        private Vector3 CheckCollision(Vector3 move)
        {
            RaycastHit2D hit = _capsule.Cast(Vector2.down, move.magnitude, _groundLayer);

            if (hit.collider.Log() != null)
            {
                CurrentSurfaceNormal = hit.normal;
                IsGrounded = true;
                _velocity = 0f;
                return Vector3.down * hit.distance;
            }
            else
            {
                IsGrounded = false;
                CurrentSurfaceNormal = Vector2.up;
                return move;
            }
        }

        private Vector3 CalculateSurfaceMove()
        {
            _inputDir = new Vector2(Input.GetAxisRaw("Horizontal"), 0f);

            Vector2 tangent = new Vector2(CurrentSurfaceNormal.y, -CurrentSurfaceNormal.x).normalized;
            return (Vector3)(tangent * _inputDir.x * 15f * Time.deltaTime);
        }
    }
}