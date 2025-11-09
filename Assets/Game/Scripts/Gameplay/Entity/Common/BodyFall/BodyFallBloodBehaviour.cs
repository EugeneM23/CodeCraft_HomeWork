// TODO: Uncomment me
// using Atomic.Entities;
// using Modules.Gameplay;
// using UnityEngine;
//
// namespace Game.Gameplay
// {
//     public sealed class BodyFallBloodBehaviour : IEntityInit, IEntityDispose
//     {
//         private const string ANIM_EVENT = "body_fall_event";
//
//         private readonly ParticleSystem bloodSFX;
//         private readonly Transform groundPoint;
//         
//         private AnimationEventReceiver _eventReceiver;
//
//         public BodyFallBloodBehaviour(ParticleSystem bloodSfx, Transform groundPoint)
//         {
//             this.bloodSFX = bloodSfx;
//             this.groundPoint = groundPoint;
//         }
//
//         public void Init(in IEntity entity)
//         {
//             _eventReceiver = entity.GetAnimationEventReceiver();
//             _eventReceiver.OnEvent += OnAnimEvent;
//         }
//
//         public void Dispose(in IEntity entity)
//         {
//             _eventReceiver.OnEvent -= OnAnimEvent;
//         }
//
//         private void OnAnimEvent(string message)
//         {
//             if (message != ANIM_EVENT)
//                 return;
//
//             this.bloodSFX.transform.parent = null; //TODO: Временно, сделать через пул и рейкасты!
//             this.bloodSFX.transform.position = this.groundPoint.position;
//             this.bloodSFX.Play(withChildren: true);
//         }
//     }
// }