using System;
using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Walks every damageable entity inside a sphere, once each, with how far out it stands.
    /// </summary>
    /// <remarks>
    /// The explosion and the flash grenade used to carry the same loop twice: the overlap query on
    /// <see cref="PhysicsLayers.Targets"/>, the collider-to-entity lookup, the set that stops a
    /// character with several colliders being hit several times, and the distance to the nearest
    /// point of the collider that drives the falloff.
    /// </remarks>
    public static class BlastAreaUseCase
    {
        private static readonly Collider[] Hits = new Collider[256];
        private static readonly HashSet<IEntity> Visited = new();

        /// <param name="accept">Optional filter per collider. An entity it turns away is not
        /// counted as visited, so another of its colliders can still let it through.</param>
        /// <param name="visit">Called with the entity and its distance from the centre as a share
        /// of the radius: 0 at the centre, 1 at the edge.</param>
        public static void ForEachDamageable(Vector3 point, float radius,
            Func<IEntity, Collider, bool> accept, Action<IEntity, float> visit)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, Hits, PhysicsLayers.Targets,
                QueryTriggerInteraction.Ignore);
            Visited.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i];
                IEntity target = FindEntity(hit);

                if (target == null || !target.HasDamageableTag() || Visited.Contains(target))
                    continue;

                if (accept != null && !accept(target, hit))
                    continue;

                Visited.Add(target);

                float distance = Vector3.Distance(point, hit.bounds.ClosestPoint(point));
                visit(target, Mathf.Clamp01(distance / radius));
            }

            Visited.Clear();
        }

        /// <summary>
        /// The entity a collider belongs to: the rigidbody's object first (the same lookup the
        /// bullet uses), then the collider's parents, for characters whose colliders sit below
        /// the entity root.
        /// </summary>
        public static IEntity FindEntity(Collider collider)
        {
            Rigidbody body = collider.attachedRigidbody;
            GameObject owner = body != null ? body.gameObject : collider.gameObject;

            if (owner.TryGetComponent(out IEntity entity))
                return entity;

            return collider.GetComponentInParent<IEntity>();
        }
    }
}
