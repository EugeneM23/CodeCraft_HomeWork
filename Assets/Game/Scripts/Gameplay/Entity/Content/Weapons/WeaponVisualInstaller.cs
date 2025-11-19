using Atomic.Elements;
using Atomic.Entities;
using Game;
using UnityEngine;

namespace Game
{
    public class WeaponVisualInstaller : SceneEntityInstaller 
    {
        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private AudioSource _audioSource;

        public override void Install(IEntity entity)
        {
            entity.GetFireEvent().Subscribe(() =>
            {
                Debug.Log("asdasd");
                _audioSource.Play();
                _particleSystem.Play();
            });
        }
    }
}