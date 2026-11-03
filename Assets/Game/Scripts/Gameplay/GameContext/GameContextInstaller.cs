using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class GameContextInstaller : MonoEntityInstaller<GameContext>
    {
        [SerializeField] private WeaponCatalog _weaponsCatalog;
        [SerializeField] private PlayerContext _playerContext;
        [SerializeField] private Transform _poolsParent;
        [SerializeField] private MonoEntity _audioPrefab;
        [SerializeField] private MonoEntity _hitEffectPrefab;
        [SerializeField] private MonoEntityWorld _entityWorld;

        [SerializeField] private MonoEntity _enemyPrefab;
        [SerializeField] private Transform _spawnPoint;

        public override void Install(GameContext context)
        {
            context.AddEntityWorld(_entityWorld);
            context.AddWeaponCatalog(_weaponsCatalog);
            context.AddPlayerContext(_playerContext);
            context.AddGameFactory(new GameFactory(_poolsParent));
            context.GetGameFactory().RegisterNewPrefab(_audioPrefab, GameFactoryID.AudioSource.ToString());
            context.AddBehaviour(new EnemyController(_enemyPrefab, _spawnPoint));
            context.AddEnemyFilter(new EntityFilter(_entityWorld, static entity => entity.HasEnemyTag()));
        }
    }

    public class EnemyController : IGameContextTick, IGameContextInit
    {
        private IEntityWorld<IEntity> _entityWorld;
        private GameFactory _gameFactory;

        private readonly MonoEntity _enemyPrefab;
        private readonly Transform _spawnPoint;

        public EnemyController(MonoEntity enemyPrefab, Transform spawnPoint)
        {
            _enemyPrefab = enemyPrefab;
            _spawnPoint = spawnPoint;
        }

        public void Init(IGameContext context)
        {
            _entityWorld = context.GetEntityWorld();
            _gameFactory = context.GetGameFactory();
        }

        public void Tick(IGameContext context, float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.M))
                SpawnUseCase.SpawnCharacter(_enemyPrefab, _spawnPoint, _entityWorld, _gameFactory);
        }
    }
}