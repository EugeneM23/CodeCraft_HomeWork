using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterInteractController : IPlayerContextInit, IPlayerContextTick
    {
        private IEntity _character;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter().Value;
        }

        public void Tick(IPlayerContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                InteractUseCase.InteractWithCharacter(_character);
            }
        }
    }
}