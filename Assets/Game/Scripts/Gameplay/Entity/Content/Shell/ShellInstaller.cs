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
        private GameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;

            //Core
            entity.AddRiggedBody(_rb);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(transform);
            entity.AddDestroyAction(new BaseAction(() => _gameContext.GetShellPool().Return(entity)));

            //LifeTime
            entity.AddLifeTime(new Cooldown(_lifeTime));
            entity.AddBehaviour(new LifeTimeBehaviour());
        }
    }
}