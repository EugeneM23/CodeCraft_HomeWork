using System.Collections.Generic;
using System.Linq;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class BuffVFXBehaviour : IEntityInit, IEntityDispose
    {
        private IReactiveList<BaseBuff> _buffsEffects;
        private readonly Dictionary<BaseBuff, ParticleSystem> _particles;

        public BuffVFXBehaviour(Dictionary<BaseBuff, ParticleSystem> particles)
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

        private void OnItemAdded(int index, BaseBuff buff)
        {
            _particles[buff].gameObject.SetActive(true);
            //_particles[buff].Play();
        }

        private void OnItemRemoved(int index, BaseBuff buff)
        {
            _particles[buff].gameObject.SetActive(false);
            //_particles[buff].Stop();
        }
    }
}