using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class SurfaceMovement2D : MonoBehaviour
    {
        [Header("Movement Settings")] [SerializeField]
        private float moveSpeed = 5f;

        [SerializeField] private float rotationSpeed = 10f;

        [Header("Ground Detection")] [SerializeField]
        private float groundCheckDistance = 0.1f;

        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private Transform groundCheckPoint;

        private Rigidbody2D rb;
        private Vector2 currentNormal;
        private bool isGrounded;

        private void Start()
        {
            rb = GetComponent<Rigidbody2D>();

            // Если не задана точка проверки земли, используем позицию объекта
            if (groundCheckPoint == null)
            {
                groundCheckPoint = transform;
            }
        }

        private void Update()
        {
            CheckGround();
            HandleInput();
        }

        private void CheckGround()
        {
            // Проверяем наличие поверхности под объектом
            RaycastHit2D hit = Physics2D.Raycast(
                groundCheckPoint.position,
                -transform.up,
                groundCheckDistance,
                groundLayer
            );

          
        }

        private void HandleInput()
        {
            //if (!isGrounded) return;

            float horizontalInput = Input.GetAxisRaw("Horizontal").Log();

            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                MoveAlongSurface(horizontalInput);
                Debug.Log("Move");
            }
        }

        private void MoveAlongSurface(float direction)
        {
            
            Vector2 moveDirection = new Vector2(currentNormal.y, -currentNormal.x);

            moveDirection *= direction;

            // Перемещаем объект
            rb.linearVelocity = moveDirection * moveSpeed;
        }

        

        private void OnDrawGizmos()
        {
            // Визуализация проверки земли в редакторе
            if (groundCheckPoint != null)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Vector3 startPos = groundCheckPoint.position;
                Vector3 endPos = startPos - transform.up * groundCheckDistance;
                Gizmos.DrawLine(startPos, endPos);

                if (isGrounded)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(groundCheckPoint.position, currentNormal * 0.5f);
                }
            }
        }
    }
}