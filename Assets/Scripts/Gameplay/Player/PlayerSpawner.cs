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
        [FormerlySerializedAs("plaerWeaponRepository")] [FormerlySerializedAs("weaponRepository")] [FormerlySerializedAs("_weaponHandler")] [SerializeField] private PlayerWeaponRepository playerWeaponRepository;

        private void Awake()
        {
            SpawnPlayer();
        }

        private void SpawnPlayer()
        {
            Character player = CreatePlayer();
            
            playerWeaponRepository.SetPlayer(player);
            player.SetBulletManagerToWeapon(_bulletManager);
            
            var playerShootController = new PlayerShootController(_input, player);
            var playerMoveController = new PlayerMoveController(_input, player);
            var playerDeathObserver = new PlayerDeathObserver(player);
            var playerWeaponSwitchController = new PlayerWeaponSwitchController(_input, playerWeaponRepository);
        }

        private Character CreatePlayer()
        {
            return Instantiate(_playerPrefab, _playerSpawnPoint.position, Quaternion.identity);
        }
    }
}