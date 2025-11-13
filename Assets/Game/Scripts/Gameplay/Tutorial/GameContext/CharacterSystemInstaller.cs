using System;
using Atomic.Contexts;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class CharacterSystemInstaller : IContextInstaller<IGameContext>
    {
        [SerializeField] private SceneEntity _character;

        public void Install(IGameContext context)
        {
            context.AddCharacter(_character);
            context.AddCharacterTransform(_character.transform);
            context.AddController<CharacterMoveController>();
            context.AddController<CharacterFireController>();
        }
    }
}