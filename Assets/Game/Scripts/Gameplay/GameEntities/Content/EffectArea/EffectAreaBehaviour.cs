using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Applies an aspect to whatever enters the area and takes it away again on the way out.
    /// </summary>
    /// <remarks>
    /// The trigger reports one event per <see cref="Collider"/>, but an aspect belongs to the
    /// entity. A character whose capsule and some child collider both cross the boundary used to
    /// get the aspect applied twice; the old AddValue ignored the second add, the new one throws
    /// and takes the rest of the composite with it. The mirror case was just as wrong and silent:
    /// one of those colliders leaving stripped the aspect while the entity was still inside.
    /// Counting colliders per entity makes Apply and Discard fire exactly once each.
    /// </remarks>
    public class EffectAreaBehaviour : IEntityInit, IEntityDispose
    {
        private readonly TriggerEventReceiver _trigger;
        private readonly IEntityAspect _entityAspect;
        private readonly Dictionary<IEntity, int> _inside = new();

        public EffectAreaBehaviour(TriggerEventReceiver trigger, IEntityAspect entityAspect)
        {
            _trigger = trigger;
            _entityAspect = entityAspect;
        }

        public void Init(IEntity entity)
        {
            _trigger.OnEntered += OnTriggerEnter;
            _trigger.OnExited += OnTriggerExit;
        }

        public void Dispose(IEntity entity)
        {
            _trigger.OnEntered -= OnTriggerEnter;
            _trigger.OnExited -= OnTriggerExit;

            // The area is going away while entities may still be standing in it, so give the
            // aspect back rather than leaving its values on them for good.
            foreach (IEntity inside in _inside.Keys)
                if (inside is { IsValid: true })
                    _entityAspect.Discard(inside);

            _inside.Clear();
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (!collider.TryGetEntity(out IEntity entity))
                return;

            // An effect area is for characters. Without this any entity that flies through gets
            // the aspect applied, and the aspects assume a character: JumpAspect builds its jump
            // condition from GetHealth().Exists, which throws for a bullet. Damageable is the tag
            // every character carries and no projectile or pickup does.
            if (!entity.HasDamageableTag())
                return;

            if (_inside.TryGetValue(entity, out int colliders))
            {
                _inside[entity] = colliders + 1;
                return;
            }

            _inside[entity] = 1;
            _entityAspect.Apply(entity);
        }

        private void OnTriggerExit(Collider collider)
        {
            if (!collider.TryGetEntity(out IEntity entity))
                return;

            if (!_inside.TryGetValue(entity, out int colliders))
                return;

            if (colliders > 1)
            {
                _inside[entity] = colliders - 1;
                return;
            }

            _inside.Remove(entity);
            _entityAspect.Discard(entity);
        }
    }
}
