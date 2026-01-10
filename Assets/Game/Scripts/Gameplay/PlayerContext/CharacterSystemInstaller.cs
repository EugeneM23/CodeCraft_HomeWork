using System;
using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class CharacterSystemInstaller : IContextInstaller<IPlayerContext>
    {
        [SerializeField] private SceneEntity _character;

        public void Install(IPlayerContext context)
        {
            context.AddCharacter(new ReactiveVariable<IEntity>(_character));
            
            context.AddController<CharacterMoveController>();
            context.AddController<CharacterRotateController>();
            context.AddController<CharacterFireController>();
            context.AddController<CharacterJumpController>();
            context.AddController<CharacterInteractController>();
            context.AddController<CharacterDropWeaponController>();
        }
    }
}