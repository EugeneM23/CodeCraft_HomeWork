using System.Buffers;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class InteractUseCase
    {
        private const int COLLIDER_BUFFER_SIZE = 32;

        public static void Interact(IEntity character, IEntity target)
        {
            if (target == null || !target.HasInteractableTag()) return;

            target.GetInteractAction().Invoke(character);
        }

        public static void InteractWithCharacter(IEntity character)
        {
            IEntity entity = character.GetTargetInteractable().Value;

            Interact(character, entity);
        }

        public static bool FindClosestItem(
            Transform origin,
            float radius,
            LayerMask layerMask,
            QueryTriggerInteraction triggerInteraction,
            out IEntity target
        )
        {
            Vector3 center = origin.position;
            Collider[] colliders = ArrayPool<Collider>.Shared.Rent(COLLIDER_BUFFER_SIZE);

            int count = Physics.OverlapSphereNonAlloc(center, radius, colliders, layerMask, triggerInteraction);

            float minDistance = float.MaxValue;
            target = null;

            for (int i = 0; i < count; i++)
            {
                Collider collider = colliders[i];

                if (!collider.TryGetEntity(out IEntity other) || !other.HasInteractableTag()) continue;

                Vector3 position = other.GetTransform().position;

                float distance = Vector3.Distance(center, position);

                if (distance >= minDistance) continue;

                target = other;
                minDistance = distance;
            }

            ArrayPool<Collider>.Shared.Return(colliders);

            // Отрисовка радиуса сферы
#if UNITY_EDITOR
            Debug.DrawLine(center, center + Vector3.forward * radius, target != null ? Color.green : Color.red);
            Debug.DrawLine(center, center + Vector3.back * radius, target != null ? Color.green : Color.red);
            Debug.DrawLine(center, center + Vector3.left * radius, target != null ? Color.green : Color.red);
            Debug.DrawLine(center, center + Vector3.right * radius, target != null ? Color.green : Color.red);
            Debug.DrawLine(center, center + Vector3.up * radius, target != null ? Color.green : Color.red);
            Debug.DrawLine(center, center + Vector3.down * radius, target != null ? Color.green : Color.red);
#endif

            return target != null;
        }

        public static void ShowUI(IEntity entity, bool show)
        {
            entity.GetShowUIAction().Invoke(show);
        }
    }
}