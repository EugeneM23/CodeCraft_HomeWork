using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class ShellInstaller : SceneEntityInstaller
    {
        [SerializeField] private Rigidbody _rb;

        [SerializeField] private float _lifeTime = 3f;
        private GameFactory _gameFactory;

        public override void Install(IEntity entity)
        {
            _gameFactory = GameContext.Instance.GetGameFactory();

            //Core
            entity.AddEntityID(GameFactoryID.Shell_01.ToString());
            entity.AddRiggedBody(_rb);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(transform);
            entity.AddDestroyAction(new BaseAction(() => SpawnUseCase.UnSpawnShell(_gameFactory, entity)));

            //LifeTime
            entity.AddLifeTime(new Cooldown(_lifeTime));
            entity.AddBehaviour(new LifeTimeBehaviour());
        }
    }
}