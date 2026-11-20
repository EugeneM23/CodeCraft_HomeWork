using System;
using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace AIKit.Entities
{
    /// <summary>
    /// The candidates a Senses sensor looks at, taken from a collection of Atomic entities (a world, a filter by tag —
    /// <c>new EntityFilter(world, e => e.HasEnemyTag())</c>) instead of a physics overlap: the entities whose object lies
    /// within the radius, and that pass <see cref="Predicate"/> if one is given. The game keeps the collection current;
    /// a scan only reads it, by index, with no allocations. An entity that is not a Unity component has no place and is
    /// skipped.
    /// </summary>
    public sealed class EntityTargetScanner : ITargetScanner
    {
        private readonly IReadOnlyEntityCollection<IEntity> _entities;
        private readonly Func<IEntity, bool> _predicate;

        public EntityTargetScanner(IReadOnlyEntityCollection<IEntity> entities, Func<IEntity, bool> predicate = null)
        {
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _predicate = predicate;
        }

        public Func<IEntity, bool> Predicate => _predicate;

        public int Scan(Vector3 center, float radius, List<Transform> results)
        {
            results.Clear();
            float limit = radius * radius;
            int count = _entities.Count;
            for (int i = 0; i < count; i++)
            {
                if (!_entities.TryGetAt(i, out IEntity entity) || entity is not Component component || component == null)
                    continue;
                if (_predicate != null && !_predicate(entity))
                    continue;

                Transform root = component.transform;
                if ((root.position - center).sqrMagnitude <= limit)
                    results.Add(root);
            }

            return results.Count;
        }
    }
}
