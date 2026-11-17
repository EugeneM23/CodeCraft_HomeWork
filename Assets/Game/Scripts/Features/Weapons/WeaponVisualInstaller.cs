using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class WeaponVisualInstaller : MonoEntityInstaller
    {
        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private CooldownTimer _fireLightCooldown;
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
        private readonly CooldownTimer _fireLightCooldown;
        private readonly GameObject _light;

        // Whether the light may be on. It used to be switched off on every frame after the cooldown,
        // a native call per frame per weapon; now once after each shot. Starts true: the light's
        // state in the prefab is not known, and the first expired frame switches it off as before.
        private bool _lit = true;

        public FireLightBehaviour(CooldownTimer fireLightCooldown, GameObject light)
        {
            _fireLightCooldown = fireLightCooldown;
            _light = light;
        }

        public void Init(IEntity entity) => entity.GetFireEvent().Subscribe(OnFire);

        public void Dispose(IEntity entity) => entity.GetFireEvent().Unsubscribe(OnFire);

        private void OnFire()
        {
            _fireLightCooldown.Reset();

            // Phones keep one pixel light (the sun), so a muzzle light would only be a per-vertex
            // light there: no visible flash on the floor, but per-object light setup on every shot.
            // The muzzle particles still flash.
            if (_light != null && !PerformanceTier.IsMobile)
            {
                _light.SetActive(true);
                _lit = true;
            }
        }

        // PT_01.prefab has no _light assigned and a zero-duration cooldown, so IsExpired() is
        // true from the first frame and this threw every frame. Neither TickableManager.Update
        // nor MonoEntity.Tick wraps the loop in a try/catch, so that throw also stopped every
        // tick source registered after this weapon -- enemies among them.
        public void Tick(IEntity entity, float deltaTime)
        {
            _fireLightCooldown.Tick(deltaTime);
            if (_lit && _fireLightCooldown.IsExpired() && _light != null)
            {
                _light.SetActive(false);
                _lit = false;
            }
        }
    }
}