using System;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class PlayerCharacterProvider
        {
            public CharacterController2D Player { get; private set; }
            
            public event Action OnPlayerChanged;
    
            public void SetCharacter(CharacterController2D character)
            {
                Player = character;
                OnPlayerChanged?.Invoke();
            }
        }
}