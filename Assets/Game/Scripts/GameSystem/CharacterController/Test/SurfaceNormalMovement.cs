using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameSystem.CharacterController.Test
{
    public class SurfaceNormalMovement : MonoBehaviour
    {
        public float speed = 5f;
        public float rayDistance = 20f;
        [SerializeField] private LayerMask layerMask;

        void Update()
        {
            float input = Input.GetAxis("Horizontal");
        
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, -transform.up, rayDistance, layerMask);
            
            if (hits.Length > 0)
            {
                // Находим ближайший хит
                RaycastHit2D closest = hits[0];
                float minDist = hits[0].distance;
                
                for (int i = 1; i < hits.Length; i++)
                {
                    if (hits[i].distance < minDist)
                    {
                        closest = hits[i];
                        minDist = hits[i].distance;
                    }
                }
                
                Vector2 normal = closest.normal;
                Vector2 moveDir = new Vector2(normal.y, -normal.x) * Mathf.Sign(input);
                transform.position += (Vector3)moveDir * speed * Time.deltaTime;
                
                float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }
}