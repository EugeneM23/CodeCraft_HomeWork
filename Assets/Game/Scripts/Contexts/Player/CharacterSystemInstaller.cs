using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class CharacterSystemInstaller : IEntityInstaller<IPlayerContext>
    {
        [SerializeField] private MonoEntity _character;

        public void Install(IPlayerContext context)
        {
            context.AddCharacter(new ReactiveVariable<IEntity>(_character));

            context.AddBehaviour<CharacterMoveController>();
            context.AddBehaviour<CharacterRotateController>();
            context.AddBehaviour<CharacterFireController>();
            context.AddBehaviour<CharacterJumpController>();
            context.AddBehaviour<CharacterInteractController>();
            context.AddBehaviour<CharacterDropWeaponController>();
        }
    }
}