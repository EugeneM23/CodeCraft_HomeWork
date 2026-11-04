using Atomic.Entities;
using System;
using Atomic.Elements;
using UnityEngine;

namespace Game
{
    [Serializable]
    public sealed class ManaSystemInstaller : IEntityInstaller<IPlayerContext>
    {
        [SerializeField]
        private Const<int> _maxMana = 10;
        
        [SerializeField, Tooltip("Seconds between mana grants. 0 restores on every physics step.")]
        private float _restoreManaPeriod = 1f;

        [SerializeField]
        private Const<int> _restoreAmount = 1;
        
        public void Install(IPlayerContext context)
        {
            context.AddMaxMana(_maxMana);
            context.AddCurrentMana(new ReactiveInt(_maxMana.Value));
            // CooldownTimer starts at 0, i.e. already expired, so the old one-argument form granted mana
            // on the very first physics step and then on every step the period allowed. Start it
            // wound up so the first grant waits out a full period.
            context.AddBehaviour(new RestoreManaBehaviour(
                new CooldownTimer(_restoreManaPeriod, _restoreManaPeriod), _restoreAmount));
        }
    }
}