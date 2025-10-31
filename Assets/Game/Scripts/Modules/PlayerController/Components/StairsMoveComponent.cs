using UnityEngine;

namespace Game.Scripts.Modules.PlayerController.Components
{
    internal class StairsMoveComponent : IMoveComponent
    {
        private readonly PlayerController _player;

        public StairsMoveComponent(PlayerController player) => _player = player;

        public void Move(Vector2 directrion)
        {
            Vector2 move = new Vector2(0, directrion.y);
            _player.transform.Translate(move * _player.Stats.MaxSpeed * Time.deltaTime);
        }
    }

    public interface IMoveComponent
    {
        void Move(Vector2 directrion);
    }
}