using System;
using Atomic.Contexts;
using Atomic.Elements;
using Game.Content;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class AbilitySystemInstaller : IContextInstaller<IPlayerContext>
    {
        [SerializeField] private DashAbilityConfig _dashAbility;
        [SerializeField] private TeleportAbilityConfig _teleportAbility;
        [SerializeField] private StunAbilityConfig _stunAbility;

        public void Install(IPlayerContext context)
        {
            context.AddAbilities(new ReactiveDictionary<string, Ability>());
            context.GetAbilities().Add(_dashAbility.name, _dashAbility.Create(context));
            context.GetAbilities().Add(_teleportAbility.name, _teleportAbility.Create(context));
            context.GetAbilities().Add(_stunAbility.name, _stunAbility.Create(context));
            context.AddController<AbilityRunner>();
        }
    }
}