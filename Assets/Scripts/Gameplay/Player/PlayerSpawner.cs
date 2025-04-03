using UnityEngine;

namespace Gameplay
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private Character _playerPrefab;
        [SerializeField] private PlayerInput _input;
        [SerializeField] private Transform _playerSpawnPoint;
        [SerializeField] private Bulletmanager _bulletManager;

        private void Awake()
        {
            SpawnPlayer();
        }

        private void SpawnPlayer()
        {
            Character player = CreatePlayer();
            
            var playerShootController = new PlayerShootController(_input, player);
            var playerMoveController = new PlayerMoveController(_input, player);
            var playerDeathObserver = new PlayerDeathObserver(player);
        }

        private Character CreatePlayer()
        {
            Character player = Instantiate(_playerPrefab, _playerSpawnPoint.position, Quaternion.identity);
            player.SetBulletManagerToWeapon(_bulletManager);

            return player;
        }
    }
}