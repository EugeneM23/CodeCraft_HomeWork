using Game.Scripts.GameObject.Player;
using Modules.PlayerController;

namespace Gameplay
{
    public class SpawnPlayerAction : CharacterDeathObserver.IAction
    {
        [Inject] private readonly PlayerSpawner _playerSpawner;

        public void Invoke(CharacterController2D character) => _playerSpawner.Spawn();
    }
}