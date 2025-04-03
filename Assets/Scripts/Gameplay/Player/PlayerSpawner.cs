using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private Character _playerPrefab;
        [SerializeField] private PlayerInput _input;
        [SerializeField] private Transform _playerSpawnPoint;
        [SerializeField] private Bulletmanager _bulletManager;
        [SerializeField] private PlayerWeaponRepository playerWeaponRepository;

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
            var playerWeaponSwitchController = new PlayerWeaponSwitchController(_input, playerWeaponRepository);
            var playerMoveCondition = new PlayerOnScreeenCondition();

            playerWeaponRepository.SetPlayer(player);
            player.SetBulletManagerToWeapon(_bulletManager);
            player.SetMoveOnScreeenCondition(playerMoveCondition);
        }

        private Character CreatePlayer()
        {
            return Instantiate(_playerPrefab, _playerSpawnPoint.position, Quaternion.identity);
        }
    }
}