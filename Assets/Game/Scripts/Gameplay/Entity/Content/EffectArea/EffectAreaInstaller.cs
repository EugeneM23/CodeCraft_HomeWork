using Atomic.Entities;
using Atomic.Extensions;
using UnityEngine;

namespace Game.Gameplay
{
    public class EffectAreaInstaller : SceneEntityInstaller
    {
        [SerializeField] private TriggerEventReceiver _triggerEventReceiver;
        [SerializeField] private ScriptableEntityAspect _entityAspect;

        public override void Install(IEntity entity)
        {
             entity.AddBehaviour(new EffectAreaBehaviour(_triggerEventReceiver, _entityAspect));
        }
    }
}