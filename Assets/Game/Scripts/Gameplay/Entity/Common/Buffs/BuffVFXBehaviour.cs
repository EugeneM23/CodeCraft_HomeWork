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

        public void Init(in IEntity entity)
        {
            _buffsEffects = entity.GetBuffsEffects();

            _buffsEffects.OnItemDeleted += OnItemRemoved;
            _buffsEffects.OnItemInserted += OnItemAdded;
        }

        public void Dispose(in IEntity entity)
        {
            _buffsEffects.OnItemDeleted -= OnItemRemoved;
            _buffsEffects.OnItemInserted -= OnItemAdded;
        }

        private void OnItemAdded(int index, BuffBase buff)
        {
            _particles[buff.Name].gameObject.SetActive(true);
            //_particles[buff].Play();
        }

        private void OnItemRemoved(int index, BuffBase buff)
        {
            _particles[buff.Name].gameObject.SetActive(false);
            //_particles[buff].Stop();
        }
    }
}