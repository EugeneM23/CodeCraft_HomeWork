using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class BuffVFXBehaviour : IEntityInit, IEntityDispose
    {
        private IReactiveList<BaseBuff> _buffsEffects;
        private readonly Dictionary<BaseBuff, ParticleSystem> _buffsParticles;

        public BuffVFXBehaviour(Dictionary<BaseBuff, ParticleSystem> buffsParticles)
        {
            _buffsParticles = buffsParticles;
        }

        public void Init(in IEntity entity)
        {
            _buffsEffects = entity.GetBuffsEffects();

            _buffsEffects.OnStateChanged += OnStateChanged;
        }

        public void Dispose(in IEntity entity)
        {
            _buffsEffects.OnStateChanged -= OnStateChanged;
        }

        private void OnStateChanged()
        {
            foreach (var (baseBuff, particleSystem) in _buffsParticles)
            {
                if (_buffsEffects.Contains(baseBuff))
                    particleSystem.Play();
                else
                    particleSystem.Stop();
            }
        }
    }
}