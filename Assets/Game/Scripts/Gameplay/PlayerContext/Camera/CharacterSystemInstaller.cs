using System;
using Atomic.Contexts;
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
            context.AddCharacter(_character);
            context.AddCharacterTransform(_character.transform);

            context.AddController<CharacterMoveController>();
            context.AddController<CharacterRotateController>();
            context.AddController<CharacterFireController>();
            context.AddController<CharacterInteractController>();
            context.AddController<CharacterDropWeaponController>();
        }
    }
}