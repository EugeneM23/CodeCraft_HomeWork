using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private Character _playerPrefab;
        [SerializeField] private PlayerInput _input;
        [SerializeField] private Transform _playerSpawnPoint;
        [SerializeField] private WeaponData _testData;
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
        }

        private Character CreatePlayer()
        {
            Character player = Instantiate(_playerPrefab, _playerSpawnPoint.position, Quaternion.identity);
            player.SetWeaponData(_testData);
            player.SetBulletManager(_bulletManager);

            return player;
        }
    }

    public class PlayerShootController
    {
        private readonly PlayerInput _input;
        private readonly Character _player;

        public PlayerShootController(PlayerInput input, Character player)
        {
            _input = input;
            _player = player;

            Subscription();
        }

        private void Subscription() => _input.OnShoot += _player.Shoot;

        private void UnSubscription() => _input.OnShoot -= _player.Shoot;
    }

    public class PlayerMoveController
    {
        private readonly PlayerInput _input;
        private readonly Character _player;

        public PlayerMoveController(PlayerInput input, Character player)
        {
            _input = input;
            _player = player;

            Subscription();
        }

        private void Subscription()
        {
            _input.OnMove += _player.Move;
        }

        private void UnSubscription() => _input.OnMove -= _player.Move;
    }
}