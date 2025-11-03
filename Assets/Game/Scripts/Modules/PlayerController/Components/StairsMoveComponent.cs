using UnityEngine;

namespace Modules.PlayerController
{
    internal class StairsMoveComponent : IMoveComponent
    {
        private readonly CharacterController2D _character;

        public StairsMoveComponent(CharacterController2D character) => _character = character;

        public void Move(Vector2 directrion)
        {
            if (CanMove()) return;

            Vector2 move = new Vector2(0, directrion.y);
            _character.transform.Translate(move * _character.Stats.MaxSpeed * Time.deltaTime);
        }

        public bool CanMove()
        {
            foreach (var item in _character.MoveCondition)
                if (!item.Invoke())
                    return false;

            return true;
        }
    }
}