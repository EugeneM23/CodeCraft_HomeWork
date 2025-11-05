using System;
using Modules.PlayerController;

namespace Gameplay
{
    public class PlayerCharacterProvider
        {
            public Character Player { get; private set; }
            
            public event Action OnPlayerChanged;
    
            public void SetCharacter(Character character)
            {
                Player = character;
                OnPlayerChanged?.Invoke();
            }
        }
}