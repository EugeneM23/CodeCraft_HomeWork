using Gameplay;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class EnemyMoveController : IMoveController, ITickable
    {
        [Inject] private readonly CharacterController2D _character;
        public Vector2 CurrentDirection { get; private set; }

        public void Tick()
        {
            _character.Move(CurrentDirection);
        }

        public void SetDirection(Vector3 transformPosition)
        {
            CurrentDirection = transformPosition - _character.transform.position;
        }
    }
}