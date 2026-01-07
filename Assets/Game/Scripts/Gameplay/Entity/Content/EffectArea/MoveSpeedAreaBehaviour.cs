using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class MoveSpeedAreaBehaviour : IEntityInit, IEntityDispose
    {
        private readonly TriggerEventReceiver _trigger;
        private readonly float _speedValue;

        public MoveSpeedAreaBehaviour(TriggerEventReceiver trigger, float speedValue)
        {
            _trigger = trigger;
            _speedValue = speedValue;
        }

        public void Init(in IEntity entity)
        {
            _trigger.OnEntered += OnTriggerEnter;
            _trigger.OnExited += OnTriggerExit;
        }

        public void Dispose(in IEntity entity)
        {
            _trigger.OnEntered -= OnTriggerEnter;
            _trigger.OnExited -= OnTriggerExit;
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (collider.TryGetEntity(out IEntity entity) &&
                entity.TryGetMoveSpeed(out IReactiveVariable<float> moveSpeed))
                moveSpeed.Value *= _speedValue;
        }

        private void OnTriggerExit(Collider collider)
        {
            if (collider.TryGetEntity(out IEntity entity) &&
                entity.TryGetMoveSpeed(out IReactiveVariable<float> moveSpeed))
                moveSpeed.Value /= _speedValue;
        }
    }
}