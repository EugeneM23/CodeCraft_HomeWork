// TODO: Uncomment me
// using Atomic.Entities;
// using Modules.Gameplay;
// using UnityEngine;
//
// namespace Game.Gameplay
// {
//     public sealed class BodyFallSoundBehaviour : IEntityInit, IEntityDispose
//     {
//         private const string FALL_ANIM_EVENT = "body_fall_event";
//
//         private readonly AudioClip _clip;
//         private AnimationEventReceiver _eventReceiver;
//         private AudioSource _audioSource;
//
//         public BodyFallSoundBehaviour(AudioClip clip)
//         {
//             _clip = clip;
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
//         private void OnAnimEvent(string message)
//         {
//             if (message == FALL_ANIM_EVENT)
//             {
//                 _audioSource.PlayOneShot(_clip);
//             }
//         }
//     }
// }