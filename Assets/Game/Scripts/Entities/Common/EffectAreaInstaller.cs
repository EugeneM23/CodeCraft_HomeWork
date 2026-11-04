using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class EffectAreaInstaller : MonoEntityInstaller
    {
        [SerializeField] private TriggerEventReceiver _triggerEventReceiver;
        [SerializeField] private ScriptableEntityAspect _entityAspect;

        public override void Install(IEntity entity)
        {
             entity.AddBehaviour(new EffectAreaBehaviour(_triggerEventReceiver, _entityAspect));
        }
    }
}
