using Gameplay;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class Enemy : IInitializeble
    {
        [Inject] private readonly CharacterController2D _character;


        public void Initialize()
        {
        }
    }
}