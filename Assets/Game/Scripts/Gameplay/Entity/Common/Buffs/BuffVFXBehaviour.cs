using System.Collections.Generic;
using System.Linq;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class BuffVFXBehaviour : IEntityInit, IEntityDispose
    {
        private IReactiveList<BuffBase> _buffsEffects;
        private readonly Dictionary<string, ParticleSystem> _particles;

        public BuffVFXBehaviour(Dictionary<string, ParticleSystem> particles)
        {
            _particles = particles;
        }

        public void Init(IEntity entity)
        {
            _buffsEffects = entity.GetBuffsEffects();

            _buffsEffects.OnItemRemoved += OnItemRemoved;
            _buffsEffects.OnItemAdded += OnItemAdded;
        }

        public void Dispose(IEntity entity)
        {
            _buffsEffects.OnItemRemoved -= OnItemRemoved;
            _buffsEffects.OnItemAdded -= OnItemAdded;
        }

        private void OnItemAdded(int index, BuffBase buff)
        {
            if (_particles.TryGetValue(buff.Name, out var particleSystem))
                particleSystem.gameObject.SetActive(true);
        }

        private void OnItemRemoved(int index, BuffBase buff)
        {
            if (_particles.TryGetValue(buff.Name, out var particleSystem))
                particleSystem.gameObject.SetActive(false);
        }
    }
}