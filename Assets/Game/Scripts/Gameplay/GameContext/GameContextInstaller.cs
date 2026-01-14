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

        protected override void Install(IGameContext context)
        {
            context.AddEntityWorld(_entityWorld);
            context.AddWeaponCatalog(_weaponsCatalog);
            context.AddPlayerContext(_playerContext);
            context.AddGameFactory(new GameFactory(_poolsParent));
            context.GetGameFactory().RegisterNewPrefab(_audioPrefab, GameFactoryID.AudioSource.ToString());
            context.AddController<EnemyController>();
        }
    }

    public class EnemyController : IContextUpdate, IContextInit<GameContext>
    {
        private IEntityWorld _entityWorld;

        public void Init(GameContext context)
        {
            _entityWorld = context.GetEntityWorld();
        }

        public void OnUpdate(IContext context, float deltaTime)
        {
            foreach (IEntity enemy in _entityWorld.GetAllWithTag(EntityAPI.Enemy))
            {
                Debug.Log(enemy.Name);
            }
        }
    }
}