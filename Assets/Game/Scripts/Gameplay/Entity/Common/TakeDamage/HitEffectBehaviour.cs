using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public class HitEffectBehaviour : IEntityInit
    {
        private Health _health;
        private readonly ParticleSystem _bulletBlood;

        public HitEffectBehaviour(ParticleSystem bulletBlood)
        {
            _bulletBlood = bulletBlood;
        }

        public void Init(in IEntity entity)
        {
            _health = entity.GetHealth();
            _health.OnHealthChanged += OnHealthChanged;
        }

        private void OnHealthChanged(int health)
        {
            _bulletBlood.Play();
        }
    }
}