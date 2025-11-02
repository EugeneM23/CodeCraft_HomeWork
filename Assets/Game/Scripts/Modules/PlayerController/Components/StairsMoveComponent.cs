using UnityEngine;

namespace Modules.PlayerController
{
    internal class StairsMoveComponent : IMoveComponent
    {
        private readonly CharacterController2D _character;

        public StairsMoveComponent(CharacterController2D character) => _character = character;

        public void Move(Vector2 directrion)
        {
            Vector2 move = new Vector2(0, directrion.y);
            _character.transform.Translate(move * _character.Stats.MaxSpeed * Time.deltaTime);
        }
    }

    public interface IMoveComponent
    {
        void Move(Vector2 directrion);
    }
}