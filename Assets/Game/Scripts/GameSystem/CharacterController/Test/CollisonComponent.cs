using System;
using UnityEngine;

namespace Game.Scripts.GameSystem.CharacterController.Test
{
    public class CollisionComponent : MonoBehaviour
    {
        [SerializeField] private CapsuleCollider2D capsule;
        [SerializeField] private LayerMask collisionLayer;

        void Start()
        {
            capsule = GetComponent<CapsuleCollider2D>();
        }

        void Update()
        {
            RaycastHit2D hit = capsule.Cast(collisionLayer);

            if (hit.collider != null)
            {
                Debug.DrawRay(hit.point, hit.normal, Color.green);
            }
        }
    }
}