using UnityEngine;

namespace Game.Scripts.GameSystem.CharacterController.Test
{
    public static class CapsuleCollider2DExtensions
    {
        public static RaycastHit2D Cast(this CapsuleCollider2D capsule, Vector2 direction, float distance,
            LayerMask layerMask)
        {
            Vector2 point = capsule.transform.position;
            Vector2 size = capsule.size;
            CapsuleDirection2D capsuleDirection = capsule.direction;
            float angle = capsule.transform.rotation.eulerAngles.z;

            return Physics2D.CapsuleCast(
                point,
                size,
                capsuleDirection,
                angle,
                direction,
                distance,
                layerMask
            );
        }

        // Перегрузка для проверки на месте (direction = 0, distance = 0)
        public static RaycastHit2D Cast(this CapsuleCollider2D capsule, LayerMask layerMask)
        {
            return capsule.Cast(Vector2.zero, 0f, layerMask);
        }
        public static RaycastHit2D CastNearest(this CapsuleCollider2D capsule, Vector2 direction, float distance,
            LayerMask layerMask)
        {
            Vector2 point = capsule.transform.position;
            Vector2 size = capsule.size;
            CapsuleDirection2D capsuleDirection = capsule.direction;
            float angle = capsule.transform.rotation.eulerAngles.z;

            // Получаем все столкновения
            RaycastHit2D[] hits = Physics2D.CapsuleCastAll(point, size, capsuleDirection, angle, direction, distance, layerMask);

            if (hits.Length == 0)
                return default; // ничего не найдено

            // Находим ближайшее столкновение
            RaycastHit2D nearest = hits[0];
            for (int i = 1; i < hits.Length; i++)
            {
                if (hits[i].distance < nearest.distance)
                {
                    nearest = hits[i];
                }
            }

            return nearest;
        }
    }
}