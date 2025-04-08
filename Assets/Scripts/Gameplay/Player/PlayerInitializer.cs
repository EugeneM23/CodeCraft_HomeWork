using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(-100)]
    public class PlayerInitializer : MonoBehaviour
    {
        [SerializeField] private Character _playerPrefab;
        [SerializeField] private PlayerInput _input;
        [SerializeField] private Transform _playerSpawnPoint;
        [SerializeField] private BulletManager _bulletManager;
        [SerializeField] private PlayerWeaponCatalog playerWeaponCatalog;
        [SerializeField] private Camera _camera;

        [SerializeField] private PlayerCharacterProvider _characterProvider;

        private void Awake()
        {
            Character player = Instantiate(_playerPrefab, _playerSpawnPoint.position, Quaternion.identity);

            _characterProvider.Setup(player);
            playerWeaponCatalog.SetPlayer(player);
            player.SetBulletManager(_bulletManager);
            player.SetMoveCondition(new OnScreenCondition(_camera));
        }
    }
}