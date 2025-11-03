using Gameplay;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class EnemyBehaviour : ITickable
    {
        [Inject] private readonly EnemyMoveController _moveController;
        [Inject] private readonly CharacterController2D _character;
        [Inject] private readonly PlayerCharacterProvider _provider;
        private readonly float _attackDistance = 7f;

        public void Tick()
        {
            var distance = Vector2.Distance(_character.transform.position, _provider.Player.transform.position);

            if (distance < _attackDistance)
            {
                _moveController.SetDirection(_provider.Player.transform.position);
            }
        }
    }
}