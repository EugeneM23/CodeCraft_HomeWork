using Atomic.Contexts;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class GameContextInstaller : SceneContextInstaller<IGameContext>
    {
        [SerializeField] private WeaponCatalog _weaponsCatalog;
        [SerializeField] private PlayerContext _playerContext;
        [SerializeField] private Transform _poolsParent;
        [SerializeField] private SceneEntity _audioPrefab;
        [SerializeField] private SceneEntity _hitEffectPrefab;
        [SerializeField] private SceneEntityWorld _entityWorld;

        [SerializeField] private SceneEntity _enemyPrefab;
        [SerializeField] private Transform _spawnPoint; 

        protected override void Install(IGameContext context)
        {
            context.AddEntityWorld(_entityWorld);
            context.AddWeaponCatalog(_weaponsCatalog);
            context.AddPlayerContext(_playerContext);
            context.AddGameFactory(new GameFactory(_poolsParent));
            context.GetGameFactory().RegisterNewPrefab(_audioPrefab, GameFactoryID.AudioSource.ToString());
            context.AddController(new EnemyController(_enemyPrefab, _spawnPoint));
        }
    }

    public class EnemyController : IContextUpdate, IContextInit<GameContext>
    {
        private IEntityWorld _entityWorld;
        private GameFactory _gameFactory;

        private readonly SceneEntity _enemyPrefab;
        private readonly Transform _spawnPoint;

        public EnemyController(SceneEntity enemyPrefab, Transform spawnPoint)
        {
            _enemyPrefab = enemyPrefab;
            _spawnPoint = spawnPoint;
        }

        public void Init(GameContext context)
        {
            _entityWorld = context.GetEntityWorld();
            _gameFactory = context.GetGameFactory();
        }

        public void OnUpdate(IContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                SpawnUseCase.SpawnCharacter(_enemyPrefab, _spawnPoint, _entityWorld, _gameFactory);
            }

            foreach (IEntity enemy in _entityWorld.GetAllWithTag(EntityAPI.Enemy))
            {
                Debug.Log(enemy.Name);
            }
        }
    }
}