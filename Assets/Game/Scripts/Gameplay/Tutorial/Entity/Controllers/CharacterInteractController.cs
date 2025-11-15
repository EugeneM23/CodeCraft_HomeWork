using System.Collections.Generic;
using Atomic.Contexts;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterInteractController : IContextInit<IPlayerContext>, IContextUpdate
    {
        private IEntity _character;

        public void Init(IPlayerContext context)
        {
            _character = context.GetCharacter();
        }

        public void OnUpdate(IContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                InteractUseCase.InteractWithCharacter(_character);
            }
        }
    }
}