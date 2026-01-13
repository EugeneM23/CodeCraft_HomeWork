using System;
using Atomic.Contexts;
using Atomic.Elements;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    [Serializable]
    public sealed class ManaSystemInstaller : IContextInstaller<IPlayerContext>
    {
        [SerializeField]
        private Const<int> _maxMana = 10;
        
        [SerializeField]
        private float _restoreManaPeriod;

        [SerializeField]
        private Const<int> _restoreAmount = 1;
        
        public void Install(IPlayerContext context)
        {
            context.AddMaxMana(_maxMana);
            context.AddCurrentMana(new ReactiveInt(_maxMana.Value));
            context.AddController(new RestoreManaBehaviour(new Cooldown(_restoreManaPeriod), _restoreAmount));
        }
    }
}