using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class SurfaceTangentDebugger : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private CapsuleCollider2D _collider;
        [SerializeField] private LayerMask _groundMask;
        [SerializeField] private float rayExtraLength = 0.2f;
        [SerializeField] private float lineLength = 1.0f;
        
        public Vector2 SurfaceNormal { get; set; }


        private void FixedUpdate()
        {
            Vector2 origin = _collider.bounds.center;
            float rayLength = _collider.bounds.extents.y + rayExtraLength;

            Debug.DrawRay(origin, Vector2.down * rayLength, Color.yellow);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, rayLength, _groundMask);

            if (hit.collider != null)
            {
                Vector2 normal = hit.normal.normalized;
                Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;

                Debug.DrawRay(origin, normal * lineLength, Color.blue);
                Debug.DrawRay(origin, tangent * lineLength, Color.green);
                Debug.DrawRay(origin, -tangent * lineLength, Color.red);
                
                SurfaceNormal = hit.normal;
            }
            else
            {
                SurfaceNormal = Vector2.up;
            }
        }

    }
}