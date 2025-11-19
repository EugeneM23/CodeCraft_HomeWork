// TODO: Uncomment me
// using System.Collections.Generic;
// using Atomic.Entities;
// using Modules.Gameplay;
// using UnityEngine;
//
// namespace Game.Gameplay
// {
//     public sealed class MoveStepSoundBehaviour : IEntityInit, IEntityDispose
//     {
//         private const string MOVE_STEP_EVENT = "move_step_event";
//
//         private readonly AudioClip[] _audioClips;
//         private readonly List<AudioClip> availableClips = new();
//
//         private AudioSource _audioSource;
//         private AnimationEventReceiver _eventReceiver;
//         
//         public MoveStepSoundBehaviour(AudioClip[] audioClips)
//         {
//             _audioClips = audioClips;
//         }
//
//         public void Init(in IEntity entity)
//         {
//             _audioSource = entity.GetAudioSource();
//             _eventReceiver = entity.GetAnimationEventReceiver();
//             _eventReceiver.OnEvent += this.OnAnimEvent;
//         }
//
//         public void Dispose(in IEntity entity)
//         {
//             _eventReceiver.OnEvent -= this.OnAnimEvent;
//         }
//
//         private void OnAnimEvent(string evt)
//         {
//             if (evt == MOVE_STEP_EVENT)
//                 this.PlayMoveStep();
//         }
//
//         private void PlayMoveStep()
//         {
//             if (this.availableClips.Count == 0)
//                 this.availableClips.AddRange(_audioClips);
//
//             int randomIndex = Random.Range(0, availableClips.Count);
//             AudioClip targetClip = this.availableClips[randomIndex];
//             this.availableClips.Remove(targetClip);
//
//             _audioSource.PlayOneShot(targetClip);
//         }
//     }
// }