using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;
using Cooldown = Modules.Gameplay.Cooldown;

namespace Game
{
    public class HitEffectInstaller : MonoEntityInstaller
    {
        [SerializeField] private float _lifeTime = 3f;
        private IGameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;
            entity.AddEntityID(gameObject.name.Replace("(Clone)", ""));
            entity.AddTransform(transform);
            // Was hardcoded 3f, which made the serialized field inert and pinned the pool turnover
            // rate: a short impact effect held its pooled slot for three seconds.
            entity.AddLifeTime(new Cooldown(_lifeTime));
            entity.AddDestroyAction(new InlineAction(() => SpawnUseCase.UnSpawnEntity(_gameContext.GetGameFactory(), entity)));
            entity.AddBehaviour<LifeTimeBehaviour>();
        }
    }
}