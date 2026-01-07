using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class MoveSpeedAreaInstaller : SceneEntityInstaller
    {
        [SerializeField] private float _speedMultiplier = 2;
        [SerializeField] private TriggerEventReceiver _triggerEventReceiver; 
        public override void Install(IEntity entity)
        {
            entity.AddBehaviour(new MoveSpeedAreaBehaviour(_triggerEventReceiver, _speedMultiplier));
        }
    }
}