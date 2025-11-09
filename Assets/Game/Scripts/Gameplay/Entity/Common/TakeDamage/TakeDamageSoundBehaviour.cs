// TODO: Uncomment me
// using System.Collections.Generic;
// using Atomic.Elements;
// using Atomic.Entities;
// using UnityEngine;
//
// namespace Game.Gameplay
// {
//     public sealed class TakeDamageSoundBehaviour : IEntityInit, IEntityDispose
//     {
//         private IReactive<TakeDamageArgs> _damageEvent;
//         private AudioSource _audioSource;
//
//         private readonly AudioClip[] audioClips;
//         private readonly List<AudioClip> availableClips = new();
//
//         public TakeDamageSoundBehaviour(AudioClip[] audioClips)
//         {
//             this.audioClips = audioClips;
//         }
//
//         public void Init(in IEntity entity)
//         {
//             _audioSource = entity.GetAudioSource();
//             _damageEvent = entity.GetDamageTakenEvent();
//
//             _damageEvent.Subscribe(this.OnDamageTaken);
//         }
//
//         public void Dispose(in IEntity entity)
//         {
//             _damageEvent.Unsubscribe(this.OnDamageTaken);
//         }
//
//         private void OnDamageTaken(TakeDamageArgs _)
//         {
//             if (this.availableClips.Count == 0)
//                 this.availableClips.AddRange(this.audioClips);
//
//             int randomIndex = Random.Range(0, availableClips.Count);
//             AudioClip targetClip = this.availableClips[randomIndex];
//             this.availableClips.Remove(targetClip);
//
//             _audioSource.PlayOneShot(targetClip);
//         }
//     }
// }