using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Gives whoever stands in the area its buffs, and takes those same buffs back on the way out.
    /// </summary>
    /// <remarks>
    /// Zone buffs have no timer (Duration 0, see TemporaryBuff). The area keeps the buff instances
    /// it handed to each entity, so it takes back exactly those: a buff of the same kind from a
    /// pickup stays. It used to apply ScriptableObject aspects, twins of the same buffs; an aspect
    /// is one asset shared by every entity, with nowhere to remember what it changed on whom, and
    /// DamageAspect took its bonus off whatever weapon was in hand on the way out.
    ///
    /// The trigger reports one event per Collider, but a buff belongs to the entity. Counting
    /// colliders per entity makes giving and taking back happen exactly once each.
    /// </remarks>
    public class EffectAreaBehaviour : IEntityInit, IEntityDispose
    {
        private readonly TriggerEventReceiver _trigger;
        private readonly BuffConfig[] _buffs;
        private readonly Dictionary<IEntity, int> _colliders = new();
        private readonly Dictionary<IEntity, List<BuffBase>> _given = new();

        public EffectAreaBehaviour(TriggerEventReceiver trigger, BuffConfig[] buffs)
        {
            _trigger = trigger;
            _buffs = buffs ?? System.Array.Empty<BuffConfig>();
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

            // The area is going away while entities may still be standing in it.
            foreach (IEntity inside in new List<IEntity>(_given.Keys))
                this.TakeBack(inside);

            _colliders.Clear();
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (!collider.TryGetEntity(out IEntity entity))
                return;

            // An effect area is for characters: a bullet or a pickup flying through has no buffs.
            if (!entity.HasDamageableTag() || !entity.HasBuffsEffects())
                return;

            if (_colliders.TryGetValue(entity, out int count))
            {
                _colliders[entity] = count + 1;
                return;
            }

            _colliders[entity] = 1;

            var given = new List<BuffBase>(_buffs.Length);
            foreach (BuffConfig config in _buffs)
            {
                if (config == null)
                    continue;

                BuffBase buff = config.CreateBuff();
                if (BuffUseCase.Apply(entity, buff))
                    given.Add(buff);
            }

            _given[entity] = given;
        }

        private void OnTriggerExit(Collider collider)
        {
            if (!collider.TryGetEntity(out IEntity entity))
                return;

            if (!_colliders.TryGetValue(entity, out int count))
                return;

            if (count > 1)
            {
                _colliders[entity] = count - 1;
                return;
            }

            _colliders.Remove(entity);
            this.TakeBack(entity);
        }

        private void TakeBack(IEntity entity)
        {
            if (!_given.Remove(entity, out List<BuffBase> given))
                return;

            // A dead character has dropped its buffs already, and its values are gone.
            if (entity is not { IsValid: true })
                return;

            foreach (BuffBase buff in given)
                BuffUseCase.Discard(entity, buff);
        }
    }
}
