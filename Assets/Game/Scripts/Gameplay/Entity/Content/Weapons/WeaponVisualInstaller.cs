using System;
using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class WeaponVisualInstaller : SceneEntityInstaller
    {
        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private Cooldown _fireLightCooldown;
        [SerializeField] private GameObject _light;

        public override void Install(IEntity entity)
        {
            entity.GetFireEvent().Subscribe(() =>
            {
                _audioSource.Play();
                _particleSystem.Play();
            });

            entity.AddBehaviour(new FireLightBehaviour(_fireLightCooldown, _light));
        }
    }

    public class FireLightBehaviour : IEntityUpdate, IEntityInit, IEntityDispose
    {
        private readonly Cooldown _fireLightCooldown;
        private readonly GameObject _light;

        public FireLightBehaviour(Cooldown fireLightCooldown, GameObject light)
        {
            _fireLightCooldown = fireLightCooldown;
            _light = light;
        }

        public void Init(in IEntity entity) => entity.GetFireEvent().Subscribe(OnFire);

        public void Dispose(in IEntity entity) => entity.GetFireEvent().Unsubscribe(OnFire);

        private void OnFire()
        {
            _fireLightCooldown.Reset();
            _light.SetActive(true);
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            _fireLightCooldown.Tick(deltaTime);
            if (_fireLightCooldown.IsExpired())
                _light.SetActive(false);
        }
    }
}