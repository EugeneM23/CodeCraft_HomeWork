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

        [Header("Pool prewarm")]
        [Tooltip("Instances made per frame, from just after the start, of what the scene's weapons and vehicles spawn: bullets, casings, impacts, muzzle smoke, blasts. 0 turns the prewarm off.")]
        [SerializeField, Min(0)] private int _prewarmPerFrame = 4;
        [Tooltip("No more instances are made in a frame that has spent this long (ms) on them. One always is.")]
        [SerializeField, Min(0f)] private float _prewarmFrameBudgetMs = 2f;
        [Tooltip("Most instances of one prefab made ahead. Each gets about its rate of fire × its lifetime, plus one.")]
        [SerializeField, Min(1)] private int _prewarmMaxPerPrefab = 24;

        public override void Install(GameContext context)
        {
            context.AddEntityWorld(_entityWorld);
            context.AddWeaponCatalog(_weaponsCatalog);
            context.AddPlayerContext(_playerContext);
            context.AddGameFactory(new GameFactory(_poolsParent));
            context.GetGameFactory()
                .RegisterNewPrefab(_audioPrefab, GameFactoryID.AudioSource.ToString(), prewarm: 8);
            context.AddBehaviour(new EnemyController(_enemyPrefab, _spawnPoint));

            // Without it the first shot, hit and blast of each kind instantiates its object mid-fight.
            if (_prewarmPerFrame > 0)
                context.AddBehaviour(new CombatPoolPrewarm(_prewarmPerFrame, _prewarmFrameBudgetMs, _prewarmMaxPerPrefab));

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