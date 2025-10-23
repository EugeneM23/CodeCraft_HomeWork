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
            Vector2 direction = move.normalized; // направление
            float distance = move.magnitude; // длина движения за кадр

            RaycastHit2D hit = _capsule.Cast(direction, distance, _groundLayer);

            if (hit.collider != null)
            {
                CurrentSurfaceNormal = hit.normal;
                IsGrounded = true;
                _velocity = 0f;

                // Перемещаемся ровно до коллайдера
                return direction * hit.distance;
            }

            IsGrounded = false;
            CurrentSurfaceNormal = Vector2.up;
            return move;
        }

        private Vector3 CalculateSurfaceMove()
        {
            _inputDir = new Vector2(Input.GetAxisRaw("Horizontal"), 0f);

            // Используем RaycastAll для получения всех коллайдеров
            RaycastHit2D[] hits = Physics2D.RaycastAll(_capsule.transform.position, Vector2.down, 1f, _groundLayer);

            if (hits.Length > 0)
            {
                // Находим ближайшую точку
                RaycastHit2D closestHit = hits[0];
                float minDistance = hits[0].distance;

                for (int i = 1; i < hits.Length; i++)
                {
                    if (hits[i].distance < minDistance)
                    {
                        minDistance = hits[i].distance;
                        closestHit = hits[i];
                    }
                }

                CurrentSurfaceNormal = closestHit.normal;
            }
            else
            {
                CurrentSurfaceNormal = Vector2.up;
            }

            Vector2 tangent = new Vector2(CurrentSurfaceNormal.y, -CurrentSurfaceNormal.x).normalized;

            return (Vector3)(tangent * _inputDir.x * 15f * Time.deltaTime);
        }
    }
}