using System.Buffers;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class DamageCastUseCase
    {
        private const int COLLIDER_BUFFER_SIZE = 32;

        public static bool Cast(Transform t, float radius, int damage, LayerMask damageLayer, IEntity weapon)
        {
            DrawSphereDebug(t.position, radius);

            Collider[] colliders = ArrayPool<Collider>.Shared.Rent(COLLIDER_BUFFER_SIZE);

            int count = Physics.OverlapSphereNonAlloc(t.position, radius, colliders, damageLayer);

            for (int i = 0; i < count; i++)
            {
                if (colliders[i].TryGetEntity(out IEntity entity) && entity.HasDamageableTag())
                {
                    entity.GetHealth().Reduce(damage);
                    entity.GetDamageTakenEvent().Invoke(new TakeDamageArgs(weapon));
                    return true;
                }
            }


            ArrayPool<Collider>.Shared.Return(colliders);
            return false;
        }

        private static void DrawSphereDebug(Vector3 pos, float radius)
        {
            const int SEGMENTS = 16;
            float delta = 360f / SEGMENTS;

            for (int i = 0; i < SEGMENTS; i++)
            {
                float a1 = Mathf.Deg2Rad * (i * delta);
                float a2 = Mathf.Deg2Rad * ((i + 1) * delta);

                // Круг в плоскости XZ
                Vector3 p1 = pos + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                Vector3 p2 = pos + new Vector3(Mathf.Cos(a2), 0, Mathf.Sin(a2)) * radius;

                Debug.DrawLine(p1, p2, Color.red, 0.1f);
            }
        }
    }
}