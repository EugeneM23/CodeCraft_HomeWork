using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class EffectAreaInstaller : MonoEntityInstaller
    {
        [SerializeField] private TriggerEventReceiver _triggerEventReceiver;

        [Tooltip("Buffs given while inside. Use configs with Duration 0, so they last until the character leaves.")]
        [SerializeField] private BuffConfig[] _buffs;

        public override void Install(IEntity entity)
        {
            entity.AddBehaviour(new EffectAreaBehaviour(_triggerEventReceiver, _buffs));
        }
    }
}
