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
            entity.AddTransform(transform);
            entity.AddLifeTime(new Cooldown(3f));
            entity.AddDestroyAction(new BaseAction(() => HitEffectUseCase.UnspawnHit(_gameContext, entity)));
            entity.AddBehaviour<LifeTimeBehaviour>();
        }
    }

    public static class HitEffectUseCase
    {
        public static void UnspawnHit(IGameContext gameContext, IEntity entity)
        {
            gameContext.GetHitEffectPool().Return(entity);
            entity.GetLifeTime().Reset();
        }
    }
}