using System;
using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;
using Cooldown = Modules.Gameplay.Cooldown;

namespace Game
{
    public class WeaponVisualInstaller : MonoEntityInstaller
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

    public class FireLightBehaviour : IEntityTick, IEntityInit, IEntityDispose
    {
        private readonly Cooldown _fireLightCooldown;
        private readonly GameObject _light;

        public FireLightBehaviour(Cooldown fireLightCooldown, GameObject light)
        {
            _fireLightCooldown = fireLightCooldown;
            _light = light;
        }

        public void Init(IEntity entity) => entity.GetFireEvent().Subscribe(OnFire);

        public void Dispose(IEntity entity) => entity.GetFireEvent().Unsubscribe(OnFire);

        private void OnFire()
        {
            _fireLightCooldown.Reset();
            if (_light != null)
                _light.SetActive(true);
        }

        // PT_01.prefab has no _light assigned and a zero-duration cooldown, so IsExpired() is
        // true from the first frame and this threw every frame. Neither TickableManager.Update
        // nor MonoEntity.Tick wraps the loop in a try/catch, so that throw also stopped every
        // tick source registered after this weapon -- enemies among them.
        public void Tick(IEntity entity, float deltaTime)
        {
            _fireLightCooldown.Tick(deltaTime);
            if (_fireLightCooldown.IsExpired() && _light != null)
                _light.SetActive(false);
        }
    }
}