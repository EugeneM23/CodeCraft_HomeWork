using Gameplay;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class Enemy : IInitializeble
    {
        [Inject] private readonly CharacterController2D _character;
        private readonly IMoveController _moveController;

        public Enemy(IMoveController moveController)
        {
            _moveController = moveController;
        }

        public void Initialize()
        {
            _character.SetComponent(_moveController);
        }
    }
}