// TODO: Uncomment me
// using System.Collections.Generic;
// using Atomic.Entities;
// using Modules.Gameplay;
// using UnityEngine;
//
// namespace Game.Gameplay
// {
//     public sealed class AttackSoundBehaviour : IEntityInit, IEntityDispose
//     {
//         private const string ATTACK_EVENT = "start_attack_event";
//
//         private readonly AudioClip[] audioClips;
//         private readonly List<AudioClip> availableClips = new();
//         
//         private AnimationEventReceiver _animationReceiver;
//         private AudioSource _audioSource;
//
//         public AttackSoundBehaviour(AudioClip[] audioClips)
//         {
//             this.audioClips = audioClips;
//         }
//
//         public void Init(in IEntity entity)
//         {
//             _audioSource = entity.GetAudioSource();
//             _animationReceiver = entity.GetAnimationEventReceiver();
//             _animationReceiver.OnEvent += this.OnAnimEvent;
//         }
//
//         public void Dispose(in IEntity entity)
//         {
//             _animationReceiver.OnEvent -= this.OnAnimEvent;
//         }
//
//         private void OnAnimEvent(string message)
//         {
//             if (message != ATTACK_EVENT)
//                 return;
//
//             //TODO: вынести в отдельный класс и выдавать рандомную дорожку
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