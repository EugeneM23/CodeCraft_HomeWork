using UnityEngine;

namespace Gameplay
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private Player _player;
        [SerializeField] private PlayerInputController _inputController;
        [SerializeField] private Transform _playerSpawnPoint;
        [SerializeField] private WeaponData _testData;

        private void Awake() => SpawnPlayer();

        private void SpawnPlayer()
        {
            Player player = CreatePlayer();

            _inputController.OnShoot += player.Shoot;
            _inputController.OnMove += player.Move;
        }

        private Player CreatePlayer()
        {
            Player player = Instantiate(_player, _playerSpawnPoint.position, Quaternion.identity);
            player.SetWeaponData(_testData);

            return player;
        }
    }
}