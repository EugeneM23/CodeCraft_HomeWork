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
            Debug.Log("Enemy move tick");
            _character.Move(CurrentDirection);
        }

        public void SetDirection(Vector3 transformPosition)
        {
            CurrentDirection = _character.transform.position - transformPosition;
        }
    }
}