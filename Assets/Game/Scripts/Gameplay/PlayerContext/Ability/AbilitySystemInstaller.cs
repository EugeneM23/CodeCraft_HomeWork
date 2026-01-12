using System;
using Atomic.Contexts;
using Game.Content;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class AbilitySystemInstaller : IContextInstaller<IPlayerContext>
    {
        [SerializeField] private DashAbilityConfig _dashAbility;

        public void Install(IPlayerContext context)
        {
            context.AddDashAbility(_dashAbility.Create(context));
            context.AddController<DashController>();
        }
    }
}