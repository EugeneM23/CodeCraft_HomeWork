// TODO: Uncomment me
// using Atomic.Elements;
// using Atomic.Entities;
// using UnityEngine;
//
// namespace Game.Gameplay
// {
//     public sealed class TakeDamageSoundTypeBehaviour : IEntityInit, IEntityDispose
//     {
//         private readonly AudioClip _meleeSFX;
//         private readonly AudioClip _bulletSFX;
//         
//         private IReactive<TakeDamageArgs> _damageEvent;
//         private IReactive<TakeDamageArgs> _deathEvent;
//         private AudioSource _audioSource;
//
//         public TakeDamageSoundTypeBehaviour(AudioClip meleeSfx, AudioClip bulletSfx)
//         {
//             _meleeSFX = meleeSfx;
//             _bulletSFX = bulletSfx;
//         }
//         
//         public void Init(in IEntity entity)
//         {
//             _audioSource = entity.GetAudioSource();
//             _damageEvent = entity.GetDamageTakenEvent();
//             _deathEvent = entity.GetDeathTakenEvent();
//             _damageEvent.Subscribe(this.OnDamageTaken);
//             _deathEvent.Subscribe(this.OnDamageTaken);
//         }
//
//         public void Dispose(in IEntity entity)
//         {
//             _damageEvent.Unsubscribe(this.OnDamageTaken);
//             _deathEvent.Unsubscribe(this.OnDamageTaken);
//         }
//
//         private void OnDamageTaken(TakeDamageArgs args)
//         {
//             IEntity source = args.source;
//             if (source == null)
//                 return;
//
//             if (source.HasMeleeTag())
//                 _audioSource.PlayOneShot(_meleeSFX);
//             else if (source.HasProjectileTag()) 
//                 _audioSource.PlayOneShot(_bulletSFX);
//         }
//     }
// }