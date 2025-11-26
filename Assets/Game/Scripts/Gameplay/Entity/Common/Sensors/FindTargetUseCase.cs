using System.Buffers;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class FindTargetUseCase
    {
        private const int COLLIDER_BUFFER_SIZE = 32;

        public static bool TryGetTarget(IEntity weapon, out IEntity target)
        {
            target = null;

            Vector3 position = weapon.GetTransform().position;
            float radius = weapon.GetDamageRadius().Value;
            LayerMask damageLayer = weapon.GetDamageLayer();

            Collider[] colliders = ArrayPool<Collider>.Shared.Rent(COLLIDER_BUFFER_SIZE);

            int count = Physics.OverlapSphereNonAlloc(position, radius, colliders, damageLayer);

            for (int i = 0; i < count; i++)
            {
                if (colliders[i].TryGetEntity(out IEntity entity) && entity.HasDamageableTag())
                {
                    target = entity;
                    return true;
                }
            }

            ArrayPool<Collider>.Shared.Return(colliders);
            return false;
        }
    }
}