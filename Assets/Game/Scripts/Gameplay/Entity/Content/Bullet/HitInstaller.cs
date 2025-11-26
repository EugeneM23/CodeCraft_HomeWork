using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class HitInstaller : SceneEntityInstaller
    {
        [SerializeField] private float _lifeTime = 3f;
        private IGameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;
            entity.AddEntityID(gameObject.name.Replace("(Clone)", ""));
            entity.AddTransform(transform);
            entity.AddLifeTime(new Cooldown(3f));
            entity.AddDestroyAction(new BaseAction(() => _gameContext.GetGameFactory().Destroy(entity)));
            entity.AddBehaviour<LifeTimeBehaviour>();
        }
    }
}